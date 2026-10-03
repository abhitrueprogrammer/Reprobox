# ReproBox — Project-Driven Linux + C# Roadmap (v2, re-scoped)

## Goal

> Build a Linux execution analyzer in C# that runs a workload and produces one honest, versioned report of what it spawned, what it cost, and what it touched. Then, only after that ships, extend it in exactly one direction: sandboxing or reproduction.

The roadmap teaches Linux and C# **through one growing product**. A phase should not exist merely because a Linux primitive is interesting or because a C# feature is worth practicing.

The old plan was really two projects glued together: an **execution analyzer** and a **sandbox/replay system**. Twenty phases of that is how projects stall at Phase 9. This version finishes the analyzer first, makes the next step a real choice instead of a queue, and moves the speculative material to a someday list.

The core product progression is:
```text
existing process/FD inspection          (done)
        ↓
reprobox run <command>
        ↓
ExecutionSession owns a workload
        ↓
descendant tracking
        ↓
process + FD observations attached to the run
        ↓
resource monitoring for the whole tree
        ↓
lifecycle control (timeout, signals, cleanup)
        ↓
file observation
        ↓
versioned execution report
        ↓
v0.1 RELEASE  ← the finish line for the committed plan
        ↓
optional enrichments (syscalls, sockets)
        ↓
ONE track: sandbox  OR  record/replay
        ↓
someday list
```

---

## Scope Rules

1. **Ship v0.1 before touching isolation.** A finished small tool beats a half-built big one, in a portfolio and in interviews.
2. **Timebox v0.1.** Pick a calendar date now. Whatever exists on that date gets cleaned up and released. Phases that did not make it move to the next release.
3. **One vertical slice at a time.** Every phase must end in something visible in the run report, never in a standalone demo.
4. **Observed is not true.** Anything gathered from `/proc` or `strace` is an observation. The code, the report, and the README all label it that way.
5. **Choose one track after v0.1.** Sandbox and replay are both large. Doing both is how the project never finishes.
6. **Advanced C# only when a real problem asks for it.** Solve it simply first. Introduce `Span<T>`, `Channel<T>`, `SafeHandle`, or P/Invoke when you can name the problem it solves.

---

## Project Integration Rule

Before turning something into a milestone, ask:

> If this feature disappeared, would a later ReproBox capability become worse or impossible?

Examples:

- **Descendant discovery:** core. Resource totals, signaling, tracing, cgroups, and cleanup all need to know which processes belong to the workload.
- **ASCII process-tree printing:** optional UI. The relationship data matters; printing it does not need to be a milestone.
- **File-descriptor discovery:** core. It later connects files, pipes, and socket inodes to workload processes.
- **A syscall summary:** useful only if it supports diagnosis or later policy work; decorative if it is only a pretty table.
- **`Span<T>` or `Channel<T>`:** use when ReproBox creates a real need, not because they appear on a language checklist.

---

## Decision Gates

These are the moments where you stop and decide, instead of drifting.

| Gate | When | Question | Options |
|---|---|---|---|
| G1 | After Phase 6 (resources) | Do the polling leaks make the totals clearly wrong? | Accept and document, or pull a minimal cgroup forward from the sandbox track |
| G2 | After Phase 7 (lifecycle) | Is `System.Diagnostics.Process` blocking something I need (process groups, controlled launch)? | Add a small native launch helper, or use a wrapper tool for now |
| G3 | At v0.1 release | Do I still want to keep going? Which track? | Sandbox track, replay track, or stop and start another project |
| G4 | After the chosen track's first phase | Is this still teaching me something? | Continue, switch tracks, or stop |

Stopping at G3 is a success, not a failure.

---

## Project Baseline

Start deliberately small:
```text
ReproBox/

├── src/ReproBox.Cli/

├── samples/

├── tests/ReproBox.Tests/

└── LEARNING.md
```

Use **.NET 10 / `net10.0`** and enable nullable reference types. Keep ReproBox primarily C#; use a tiny C/Rust helper only if a Linux primitive becomes awkward or unsafe to own from managed code.

Do not add abstractions before you need them. Start with ordinary managed APIs, then cross into native Linux APIs only when a phase requires it.

Keep four layers separate from the beginning, because every later phase depends on it:
```text
CLI parsing        (System.CommandLine, nothing else)

Orchestration      (ExecutionSession: owns the run and its lifecycle)

Observers          (descendants, resources, files; each produces observations)

Parsing / readers  (pure functions over /proc and strace text; unit-testable)
```

---

## C# Learning Track

The C# progression happens alongside the Linux progression:
```text
File APIs + records

        ↓

robust parsing

        ↓

async/await + CancellationToken + Task.WhenAll

        ↓

System.Diagnostics.Process in depth

        ↓

PeriodicTimer / IAsyncEnumerable<T>

        ↓

System.Text.Json + schema versioning

        ↓

Span<T> / allocation awareness  (only where parsing is genuinely hot)

        ↓

LibraryImport / P/Invoke  (when signals or process groups need it)

        ↓

SafeHandle + native resource ownership  (sandbox track)

        ↓

NativeAOT + shipping a Linux-native CLI  (someday)
```

**Rule:** do not use an advanced C# feature just because it is impressive. First solve the problem simply; introduce the feature when you can explain what problem it solves.

---

# PART A — v0.1: The Execution Analyzer (committed scope)

## Current State

Phases 1 and 2 are complete. Keep their user-facing `inspect` command because it is useful for debugging, but from Phase 3 onward their main role is reusable internals for executions that ReproBox itself launches.

---

## Phase 1 — Process Inspection — **Completed**

**Project role:** reusable process-observation primitive.

Build:
```bash
reprobox inspect <PID>
```

Reads `/proc/<pid>/status`, `cmdline`, `exe`, and `cwd`. Displays PID, PPID, name, command line, executable, working directory, user, thread count, and memory usage.

Important lessons already learned:

- `cmdline` is NUL-separated.
- Processes can disappear while you are reading `/proc`, so missing data must be normal, not exceptional.
- `/proc` is a live kernel interface, not a directory of static files.

### Checkpoint

PID vs PPID, process vs thread, virtual memory, why `/proc` exists, what happens when a shell launches a program.

---

## Phase 2 — File Descriptors — **Completed**

**Project role:** reusable descriptor observation for files, pipes, terminals, and later socket attribution.

Build:
```bash
reprobox inspect <PID> --fds
```

Enumerates `/proc/<pid>/fd/` and resolves each entry (terminals, regular files, pipes, `socket:[inode]`).

Important lesson: observing another process's descriptor is not the same as owning a native handle. Never close what you do not own.

### Checkpoint

stdin/stdout/stderr, what a file descriptor is, why sockets are file descriptors, how shell redirection works.

---

## Phase 3 — Execution Core and Runner

**Project goal:** make ReproBox own a workload from start to finish. This is the spine every later phase attaches to.

Keep it narrow: launch one root process correctly, capture its result faithfully, and establish the session/result model before adding any monitoring.

Build:
```bash
reprobox run command args...
```

Record: root PID, working directory, environment snapshot, start time, end time, exit code, stdout, stderr, and a run status.

### Build in this order (smallest, most valuable first)

1. **Faithful output capture.** Read stdout and stderr as complete streams, not line by line, so ReproBox never adds or removes newlines. A reproducibility tool that alters the output it records is broken at the root.
2. **Concurrent waiting.** Output reads and process exit progress together so a chatty stderr cannot block stdout.
3. **Honest exit codes.** Command not found is 127, found but not executable is 126. A failure to launch must never be confused with the program failing.
4. **Cancellation done right.** Ctrl+C kills the whole tree, waits for exit, still collects whatever output existed, and marks the run as cancelled through a status, not through a faked exit code.
5. **A thin session object.** It owns the run: ID, command, arguments, working directory, environment snapshot, status (created, running, exited, failed, cancelled). The result is a separate immutable record of what came out.
6. **Working directory option** so runs are reproducible from anywhere.

### Experiments

```bash
reprobox run sleep 10
reprobox run ls /
reprobox run python crash.py
reprobox run nope
reprobox run sh -c "echo hi; echo err >&2; exit 3"
```

Also:

- Press Ctrl+C during `sleep 10` and confirm the result still prints.
- Run a command with flags to see how the CLI parser treats them, and decide whether a `--` separator is needed.
- Run a command that starts a background process that keeps the output pipe open, and watch what happens to the wait. Decide consciously how you want to handle it.

### C# Focus

- `System.Diagnostics.Process` and `ProcessStartInfo` properly: `UseShellExecute`, argument lists vs one string, environment, working directory, redirected streams.
- `WaitForExitAsync`, `Task.WhenAll`, and threading a `CancellationToken` through instead of inventing custom flags.
- Understand why you do not pass the cancellation token to the stream reads if you want partial output after a cancel.
- Typed immutable records for results instead of loosely related values.
- Nullable reference types: treat the warnings as design feedback, not noise.

### Checkpoint

Explain:
```text
shell
  ↓
ReproBox
  ↓
child process
  ↓
kernel
```
Including who calls `fork`/`exec`, who owns the pipes, and why only the parent can collect the exit status.

**Done when:** all experiments behave sensibly, output is byte-faithful, and the session/result split exists.

---

## Phase 4 — Workload Process Discovery

**Project goal:** determine which processes belong to the execution ReproBox launched. Build descendant tracking that later subsystems consume; do not build tree printing as the milestone.

```text
ExecutionSession
    └── root PID
          ├── child PID
          │     └── grandchild PID
          └── child PID
```

### Approach for this phase: a polling observer

A background task runs alongside the workload, like a guard doing rounds. Every ~10ms it asks "who are the children of everything I already know about?", records anything new with a first-seen time, and goes back to sleep. When the run ends it does one final round, then returns its notebook.

Design rules:

- **Remember everything seen.** Keep expanding from all known processes each round, so when a middle process dies and its children are reparented away, the ones already seen are not lost.
- **Reads may fail quietly.** A process vanishing mid-read is normal. The reader returns "nothing," never an error, and never prints to the console during a run (it would pollute the captured output).
- **Read expensive details once per process,** when first discovered, not every round.
- **Prefer the kernel's per-thread children list** when available; fall back to scanning all of `/proc` for matching parents only when it is not.
- **Stop cleanly.** The observer swallows its own cancellation and returns what it has, so the caller never handles an exception just to get the data.
- Label everything "observed, best-effort" in the code and in the output.

Known leaks, accepted for now and written down:

- Processes that live and die between two rounds are invisible.
- Orphans reparented before they were seen escape the parent chain.
- PIDs can be reused; record start time and leave a note for later.

These are exactly why cgroups exist. Feeling this limitation first is the point.

### Experiments

```bash
reprobox run sleep 2                       # one process
reprobox run sh -c "sleep 1 & sleep 1"     # shell plus two sleeps
reprobox run sh -c "sh -c 'sleep 1'"       # nesting depth
reprobox run ls /                          # may show almost nothing: the known leak
```

Then compare against `pstree` while a real workload runs (for example a dev server or a build tool) and note what your observer missed.

### C# Focus

- Long-running background tasks with linked cancellation tokens.
- `Dictionary<int, T>` and sets keyed by PID; queue-based tree expansion.
- Keep reading `/proc` separate from tree construction so each can be tested alone (use saved samples).
- Optional: implement traversal once imperatively and once with LINQ to see what LINQ hides.

### Checkpoint

Understand `fork()` vs `exec()`, why children get reparented when a parent dies, and why polling can never be ground truth.

**Done when:** the shell-with-two-sleeps case shows all three processes, and the run result carries an observed-processes list.

---

## Phase 5 — Attach Process and FD Observations to the Execution

**Project goal:** stop treating Phases 1-2 as standalone demos. Reuse them while a session is active.

For each observed workload process, attach: PID/PPID, name, command line, executable, working directory, user, threads, and a memory snapshot. File descriptors are attached as a second, separate observation.

Decide explicitly which values are:

- **Static for a process's life** (executable, command line): read once.
- **Sampled** (memory, threads): read periodically, keep summaries.
- **Event-like** (descriptors opened and closed): snapshot, and accept that you will miss some.

Do not rescan every expensive field at maximum frequency.

### Experiments

Use a controlled sample that opens regular files, creates a pipe, opens a socket, and sleeps. Run it through `reprobox run` and verify the same information you previously inspected manually appears inside the run. Also run a workload that spawns a child, to verify observations exist for more than the root.

### C# Focus

- Reuse the existing snapshot and descriptor records instead of duplicating parser output.
- Update observations by PID without rebuilding unrelated state.
- Make missing data normal: a short-lived descendant may be gone between discovery and inspection.
- Keep ownership clear: observing is not owning.

### Checkpoint

Why `/proc` observations are snapshots not truth, which process information changes during a run, why a PID can disappear between two reads, why descriptor data matters later for files and sockets.

---

## Phase 6 — Resource Monitoring

**Project goal:** measure the cost of the **whole execution**, using the membership from Phase 4. Output must feed the run result, not exist as a standalone monitor.

Track: wall-clock time, CPU time, resident memory (with peak), virtual memory, threads, context switches, disk reads, disk writes.

Sources: `/proc/<pid>/stat`, `status`, `io`.

Design rules:

- Sample while the run is alive on a steady timer, not a sleep loop that drifts.
- **Keep peaks, totals, and averages incrementally.** Do not store every sample forever.
- Sum across the observed tree, and be explicit that totals for processes that died between samples are lost. This is a known undercount.
- Give every number a unit in its name or type. Ticks, pages, bytes, milliseconds, and percentages must never be bare numbers.

### Experiments

Four controlled fixtures, compared side by side: one that burns CPU for a known time, one that allocates a known amount of memory and holds it, one that writes and reads a known amount of disk, and one that just sleeps. Also compare:
```bash
reprobox run sha256sum huge-file.iso
reprobox run sleep 10
```

Check your numbers against `top`/`/usr/bin/time -v` for the same workloads. When they disagree, find out why.

### C# Focus

- `PeriodicTimer`, `Stopwatch`, async loops with cancellation.
- `IAsyncEnumerable<ResourceSample>` if streaming samples genuinely helps; a simple callback or accumulator is fine if it does not.
- Value types with units instead of raw `long`s.
- Incremental statistics (peak, average) without unbounded memory.

### Checkpoint

CPU time vs wall time, RSS vs virtual memory, blocking I/O, context switches, peak vs current memory.

### Gate G1

After this phase, compare your totals with `/usr/bin/time -v` on a workload that spawns many short-lived children. If the undercount is large enough to mislead, pull a minimal cgroup forward (see sandbox track, S1). Otherwise document the limitation and continue.

**Done when:** each fixture produces numbers that are believable against an independent tool, and the limitation is documented.

---

## Phase 7 — Lifecycle Control: Timeouts, Signals, Cleanup

**Project goal:** make stopping a workload a first-class part of the session lifecycle. ReproBox owns the workload, so it must be able to stop it, escalate, and clean up the whole tree.

Build, inside `run` (not as separate commands against other runs):
```bash
reprobox run --timeout 30s command
```

Behavior to implement:

- **Graceful first:** on timeout or interrupt, send a polite termination signal to the whole workload.
- **Escalate:** after a grace period, force-kill whatever is left.
- **Report honestly:** the result distinguishes "exited on its own," "stopped by ReproBox after timeout," and "interrupted by the user," using status, not exit-code tricks.
- **Leave nothing behind:** after the run, verify no observed process is still alive.

Important: signalling only the root process is not enough. Learn process groups here, because "stop the workload" must mean "stop everything it started."

### Experiments

- A sample that handles the termination signal, records that it received it, and exits cleanly. Compare with a forced kill.
- A sample that ignores the polite signal, to exercise the escalation path.
- A sample that spawns children, then stop it and check nothing survives.
- Test what happens to children when the parent exits first.

### C# Focus

- Modern source-generated P/Invoke (`[LibraryImport]`) for small libc calls such as sending a signal.
- A signal enum instead of scattered integer constants.
- Translating native errors into meaningful .NET errors.
- Keep all native calls behind one tiny boundary type so the rest stays ordinary managed C#.

### Gate G2

`System.Diagnostics.Process` cannot put the child into its own process group or control what happens between fork and exec. If this phase fights you, either launch through a small wrapper tool that sets up the group, or write a tiny native launch helper. Decide here, not later; the sandbox track needs the same capability.

### Checkpoint

Why a forced kill cannot be handled, graceful shutdown, why Ctrl+C works, process groups, what happens to children when a parent exits.

---

## Phase 8 — Filesystem Observation

**Project goal:** answer "what files did this execution reference, read, create, or modify?" and feed that into the report.

Build:
```bash
reprobox run --trace-files command
```

Use `strace` as an external producer (follow children, file-related syscalls, write to a log). Then **parse it yourself** into typed events and aggregate into your own model:
```text
Files read / opened

Files created / modified

Files that were probed but did not exist
```

Do not dump raw `strace` output. Do not assume file-related tracing captures every `read()` or `write()`; it mainly captures syscalls that involve paths.

Design rules:

- Treat `strace` as slow and intrusive; document the overhead.
- Normalize paths (relative to working directory, resolved `..`) before aggregating.
- Keep raw events, aggregated sets, and terminal formatting as three separate layers.
- Stream the log; never load a giant trace into memory.
- Handle interleaved and unfinished/resumed lines when tracing multiple processes.

### Experiments

- A file fixture that reads one known input, creates one known output, stats another path, and optionally memory-maps a file. Verify your model matches what you built it to do.
- A trace of a common tool (a compiler, package manager, or test runner) and a look at what dependencies it touches that you did not expect.
- Compare the "files read" set across two runs of the same deterministic fixture.

### C# Focus

- Typed trace event records; a simple parser first, then (only if it matters) a span-based comparison.
- Streaming line reads and incremental aggregation.
- `HashSet<T>` with an explicit comparer for normalized paths.
- Introduce `Channel<T>` only if parsing and aggregation genuinely need to run concurrently and you can explain the backpressure problem it solves.

### Checkpoint

What `openat`, `stat`, `read`, `write`, and `mmap` mean; shared libraries; why a process can depend on a file it never explicitly reads.

---

## Phase 9 — Versioned Execution Report

**Project goal:** turn one observed execution into a durable, machine-readable artifact, plus a readable terminal summary. This is where the data model gets decided, which is why it comes before any isolation work.

Build:
```bash
reprobox run --report run.json command
```

Store at least: schema version, command, arguments, working directory, environment snapshot, timing, exit status and run status, observed processes, resource summary, file observations.

Rules:

- **Explicit schema version from day one.** Treat the report shape as an API that must stay compatible.
- **Do not serialize internal objects automatically.** Define the persisted shape deliberately; keep internal models separate once they diverge.
- **Observations are labeled** as observed/best-effort; inferred data is a different, explicitly marked section later.
- **Partial reports are valid.** A crashed or interrupted run still finalizes a report with whatever was collected, plus the failure state.
- Keep the report immutable after finalization.

### Experiments

- Run the same deterministic fixture twice and diff the reports. Decide which differences are noise (timestamps, PIDs) and which are signal.
- Run `sleep 2` and verify irrelevant sections stay small instead of filling with noise.
- Run a crashing process and verify a complete report is still written.
- Interrupt a workload and confirm the report separates a normal exit from a ReproBox-initiated stop.

### C# Focus

- `System.Text.Json` with an explicit version field and deliberate property naming.
- Separating domain models from persisted DTOs.
- Immutable records for finalized data.
- Streaming for large sections instead of building enormous strings.

### Checkpoint

Snapshot/event data vs final summary data, internal model vs file format, why schema versioning matters, which report fields are facts and which are future inferences.

---

## Milestone 1 — ReproBox v0.1 Release: Execution Analyzer

At this point `reprobox run` is the unifying command and everything is a property of one execution.

```bash
reprobox run --report run.json --trace-files npm test
```

reports:
```text
exit status and run status

observed process tree

CPU, memory, and I/O for the whole run

files read, created, and modified

labels on everything that is best-effort
```

### Release checklist

- README with a short demo and an honest limitations section
- Sample workloads under `samples/` used as test fixtures
- Parser tests using saved `/proc` and `strace` samples (no live PID required)
- `LEARNING.md` entries for every phase (Linux concept and C# concept)
- Tagged v0.1

### Gate G3

Do you still want to keep going? If yes, pick **one** track below. If no, you have a complete, finished project. That is already a strong result.

---

# PART B — Optional Enrichments (v0.2)

Small additions to the same run report. Do these only if they help you, not to fill space.

## Phase 10 — Syscall Summary

**Project goal:** add syscall evidence that is useful for diagnosis, not just a pretty table.

Build:
```bash
reprobox run --trace-syscalls command
```

Use `strace`'s summary mode, parse it into your own structured model (counts and time per syscall), and attach it to the report.

### Experiments

Write "Hello World" in C, C#, and Python and compare the syscall patterns. Find out why a language runtime makes hundreds of calls before your code runs.

### C# Focus

- Aggregation model separate from parsing and from formatting.
- LINQ for sorting and projection once the raw parser is correct.
- Benchmark only if you have a real performance question.

### Checkpoint

Syscall vs normal function call, userspace vs kernel space, libc vs kernel, why `Console.WriteLine()` eventually causes syscalls.

## Phase 11 — Network Sockets

**Project goal:** attach socket state to the workload by reusing the descriptor data, not as a separate networking toy.

Read the kernel's TCP/UDP socket tables, match socket inode numbers to the descriptors you already observe, and show state, local address, remote address, and owning process.

Important: this shows socket **state**, not a history of connections. Events like connect/accept/bind need syscall tracing.

### Experiments

A local client/server fixture so expected LISTEN and ESTABLISHED states are deterministic and never depend on the public internet. Then compare with `ss`.

### C# Focus

- `IPAddress`, `BinaryPrimitives`, and hexadecimal parsing.
- Implement the endianness conversion yourself once before using a helper.
- Dictionary/set joins between inodes and descriptors.

### Checkpoint

TCP vs UDP, listening vs connected, localhost, ports, DNS, client/server, why socket tables are snapshots.

---

# PART C — Pick ONE Track

Make the choice at Gate G3. Gate G4 after the first phase of the chosen track lets you change your mind.

## Track S — Sandbox

Goal: run the same session pipeline inside a restricted environment. Isolation wraps the existing execution path; it must not become a second architecture.

### S1 — cgroups v2 (do this first)

**Why first:** it gives exact process membership (fixing the polling leaks), accurate resource totals, and the ability to set limits, all from one mechanism.

Add:
```bash
reprobox run --memory 512M command
reprobox run --cpu 0.5 command
```

Learn the control files for memory limit, CPU quota, and process membership. Do not assume you can write anywhere under the cgroup tree; learn **delegation** (on a systemd distro, run workloads inside a delegated scope instead of fighting permissions).

Experiments: a memory-eater fixture run under a small limit; observe exactly what happens when it exceeds it. Compare the cgroup's membership list with what your poller observed.

C# focus: typed limit values instead of raw strings, a small lifecycle object (create, configure, attach, clean up), idempotent cleanup so failed runs do not leave stale groups.

Checkpoint: namespaces control what a process can **see**; cgroups control what it can **consume**.

### S2 — Namespaces

Experiment manually with `unshare` and `nsenter` first. Learn them one at a time: hostname, process IDs, mounts, network, users. Start by having ReproBox invoke the existing tools; only write a native helper if you must.

C# focus: Linux-only boundaries, keeping platform-specific code isolated, `SafeHandle` if namespace descriptors become long-lived.

Checkpoint: containers are isolated processes, not miniature virtual machines; why PID namespaces behave differently from changing a hostname.

### S3 — Filesystem Isolation

Manual experiments first: mount namespaces, bind mounts, tmpfs, `chroot`, `pivot_root`, OverlayFS. Target a base layer plus a writable layer presented as one root.

C# focus: deterministic cleanup, explicit path validation, testing the mount plan separately from executing it.

### S4 — Network Isolation

Start with `--no-network` using a fresh network namespace. Then experiment with virtual interface pairs, addresses, and routing using the standard `ip` tools before considering raw netlink.

C# focus: a reusable external-command runner, typed network configuration steps, `System.Net` types instead of raw strings.

## Track R — Record and Replay

Goal: use the versioned report to reconstruct a sufficiently similar environment and rerun the workload. Aim for "similar environment, similar behavior," never bit-for-bit reproduction.

### R1 — Record

Capture command, arguments, working directory, environment, input file identities (hashes, not copies, at first), and the observation report as a recorded run with an ID.

C# focus: streaming I/O, SHA-256 hashing, content-addressed storage concepts, schema versioning and migration of saved runs.

### R2 — Replay

Rebuild the environment from a recorded run and execute it again through the same session pipeline.

### R3 — Compare

Diff two reports (original vs replay) and explain what differs and why: timing noise, different PIDs, missing files, different environment. This is where the report design pays off.

---

# PART D — Someday List

Only if you are still having fun and the earlier parts are finished. None of these are committed.

- **Linux security:** inspect capabilities, drop them, `no_new_privs`, then seccomp, then Landlock. Study separately; never add all at once. Understand the difference between isolation, resource limiting, privilege reduction, and syscall filtering.
- **eBPF observation backend:** start with `bpftrace`; replace earlier mechanisms only where it clearly wins. Keep the higher-level event model stable so the backend can change.
- **Dependency inference:** turn observations into a dependency model. Keep **observed facts** and **inferred requirements** in separate types. An environment variable that merely existed is "environment supplied," not "used."
- **Generated sandbox policies:** produce a candidate policy from observations. It is an observed candidate, never a guaranteed-complete security policy: a different code path may need files, syscalls, or network access the learning run never saw.
- **NativeAOT:** a trimmed single-binary Linux release, fixing reflection assumptions.

---

## Where to Stop

You do not need everything. A strong portfolio release is already:
```text
✓ run commands and capture them faithfully

✓ inspect processes and file descriptors

✓ track the process tree (with honest limits)

✓ measure CPU, memory, and I/O for the whole run

✓ stop workloads gracefully and clean up

✓ trace file activity

✓ export a versioned execution report
```

Everything after Milestone 1 is a bonus.

---

## Known Limitations to State in the README

- Descendant tracking by polling is best-effort and can miss short-lived or orphaned processes.
- Resource totals can undercount for the same reason.
- `strace` adds overhead and slows the workload.
- `/proc` data is a snapshot of a live kernel interface, not durable truth.
- Linux only.

---

## How to Learn Instead of Copy-Pasting

For every phase:
```text
1. Read how the Linux primitive works.

2. Use the existing Linux tool manually.

3. Predict what will happen.

4. Run an experiment.

5. Explain the result in your own words.

6. Design the ReproBox feature.

7. Implement it yourself.

8. Write tests.

9. Break it deliberately.

10. Debug it.

11. Ask for help with specific things you do not understand.

12. Refactor only after it works: ask whether a C# feature solves a real problem you actually hit.

13. Write both the Linux concept and the C# concept in LEARNING.md.
```

Do not ask:
```text
"Implement Phase 7 for me."
```

Prefer:
```text
"I expected the child process to show up here but it doesn't. What assumption am I getting wrong?"
```

---

## Existing Tools to Learn First

Before implementing a feature, understand the Linux tool that exposes the same concept.

| ReproBox feature | Explore first |
| --- | --- |
| Processes | `ps`, `/proc` |
| Process tree | `pstree` |
| File descriptors | `lsof`, `/proc/*/fd` |
| Resource usage | `top`, `/usr/bin/time -v` |
| Signals | `kill` |
| Syscalls and files | `strace` |
| Network | `ss` |
| Memory | `pmap`, `/proc/*/maps` |
| Namespaces | `unshare`, `nsenter` |
| Mounts | `mount`, `findmnt` |
| cgroups | `/sys/fs/cgroup`, `systemd-run` |
| Capabilities | `capsh`, `getcap` |
| eBPF | `bpftrace` |

The goal is not to rewrite these tools. The goal is to understand the primitives underneath them and combine them into something useful. Other tools (strace, bubblewrap, Docker) already cover large parts of this space; ReproBox exists to learn Linux by building, not to replace them.

---

## Testing Strategy

Keep small deterministic sample programs under `samples/`. They are **test fixtures for ReproBox**, not tutorials.

| Fixture | Used by | Behavior |
| --- | --- | --- |
| Process sample | Phases 4, 5, 7 | Spawns a child and grandchild, then waits |
| Descriptor sample | Phase 5, 11 | Opens regular files, a pipe, and a socket, then sleeps |
| CPU sample | Phase 6 | Burns CPU for a known duration |
| Memory sample | Phase 6, S1 | Allocates and holds a known amount |
| Disk sample | Phase 6 | Writes and reads a known amount |
| Sleep sample | Phase 6 | Does nothing; baseline |
| Signal sample | Phase 7 | Handles the polite termination signal and records it |
| Stubborn sample | Phase 7 | Ignores the polite signal to test escalation |
| File sample | Phase 8 | Reads one input, writes one output, stats another path |
| Network sample | Phase 11 | Local client/server pair with deterministic socket states |
| Crash sample | Phases 3, 9 | Exits non-zero with output on stderr |

Parser tests use saved `/proc` and `strace` samples so most tests never need a live process.

---

## Immediate Next Work

You are inside **Phase 3**. In order:

1. Make output capture byte-faithful and concurrent.
2. Add honest exit codes for not-found and cannot-execute.
3. Add the session object and the status/result split.
4. Build the process fixture (child plus grandchild).
5. Move to Phase 4 and add the polling observer.
6. Pick your v0.1 date and write it at the top of `LEARNING.md`.

---

## What Changed from the Old Plan

- **Twenty phases became nine committed phases plus a release.** Everything past that is optional or a choice.
- **Runner and descendants are now a clear pair of phases** with concrete build orders, instead of one vague phase.
- **Lifecycle control moved earlier and narrowed** to timeouts, escalation, and cleanup inside `run`, because every later phase depends on stopping a workload cleanly.
- **The report moved before isolation** so the data model is decided early.
- **Syscalls and sockets became optional enrichments** instead of mandatory milestones.
- **Sandbox and replay became a choice, not a queue.** One track, with a gate after its first phase.
- **cgroups now lead the sandbox track** because they fix the weakest part of the analyzer.
- **Decision gates, a timebox, honest limitations, and a fixture table** were added so the project can actually finish.
- **Security, eBPF, dependency inference, policy generation, and NativeAOT moved to a someday list.**