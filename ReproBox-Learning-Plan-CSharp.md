# ReproBox: Project-Driven Linux + C# Roadmap (v3)

## Goal

> Build a Linux execution analyzer in C# that runs a workload and produces one honest, versioned report of what it spawned, what it cost, and what it touched. Then, only after that ships, extend it in exactly one direction: sandboxing (limits, namespaces) or reproduction.

The roadmap teaches Linux and C# **through one growing product**. A phase should not exist merely because a Linux primitive is interesting or because a C# feature is worth practicing.

**What changed in v3:** cgroup v2 moved into Phase 4. Workload membership and resource accounting are now kernel-authoritative instead of polled guesses. That shrinks the leak list, retires Gate G1, simplifies cleanup, and trims the sandbox track.

```text
existing process/FD inspection          (done)
        ↓
reprobox run <command>
        ↓
ExecutionSession owns a workload
        ↓
membership: polling first (4a), then cgroup v2 (4b)
        ↓
process + FD observations attached to the run
        ↓
resource monitoring (cgroup counters + /proc cross-check)
        ↓
lifecycle control (timeout, signals, cgroup kill, cleanup)
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

1. **Ship v0.1 before touching isolation.** A finished small tool beats a half-built big one.
2. **Timebox v0.1.** Pick a calendar date now. Whatever exists on that date gets cleaned up and released. Phases that did not make it move to the next release.
3. **One vertical slice at a time.** Every phase ends in something visible in the run report, never in a standalone demo.
4. **Label the source of truth.** Anything from `/proc` or `strace` is an *observation* (best-effort). Cgroup membership and counters are *kernel-authoritative at sample time*. The code, report, and README label which is which.
5. **Choose one track after v0.1.** Sandbox and replay are both large. Doing both is how projects never finish.
6. **Advanced C# only when a real problem asks for it.** Solve it simply first. Introduce `Span<T>`, `Channel<T>`, `SafeHandle`, or P/Invoke when you can name the problem it solves.

---

## Project Integration Rule

Before turning something into a milestone, ask:

> If this feature disappeared, would a later ReproBox capability become worse or impossible?

- **Workload membership:** core. Resource totals, signaling, tracing, and cleanup all need to know which processes belong to the workload.
- **ASCII process-tree printing:** optional UI. The relationship data matters; printing it does not need to be a milestone.
- **File-descriptor discovery:** core. It later connects files, pipes, and socket inodes to workload processes.
- **A syscall summary:** useful only if it supports diagnosis or later policy work; decorative otherwise.
- **`Span<T>` or `Channel<T>`:** use when ReproBox creates a real need, not because they appear on a checklist.

---

## Decision Gates

| Gate | When | Question | Options |
| --- | --- | --- | --- |
| G1 | *Retired.* | Was: do polling leaks make totals wrong? | Cgroup counters in Phase 4/6 answer this. |
| G2 | **Phase 3** (pulled forward) | How does the workload get launched *into* its cgroup? | A: systemd user scope. B: create cgroup, move PID in. C: native launch helper. |
| G3 | At v0.1 release | Do I still want to keep going? Which track? | Sandbox track, replay track, or stop and start another project |
| G4 | After the chosen track's first phase | Is this still teaching me something? | Continue, switch tracks, or stop |

Stopping at G3 is a success, not a failure.

---

## Requirements and Environment

- **Linux only**, with **cgroup v2** (unified hierarchy) and **delegation** working for your user (a systemd user scope is the easy path).
- .NET 10 / `net10.0`, nullable reference types enabled.
- `strace` installed (Phase 8).
- A fallback exists (polling) when cgroups are unavailable, but it is the degraded path, and the report says so.

A "does this machine support cgroup v2 + delegation" check runs in Phase 3, on day one. If it fails, fix the environment before Phase 4.

---

## Project Baseline

```text
ReproBox/
├── src/ReproBox.Cli/
├── samples/
├── tests/ReproBox.Tests/
└── LEARNING.md
```

Keep ReproBox primarily C#; use a tiny C/Rust helper only if a Linux primitive becomes awkward or unsafe to own from managed code. Do not add abstractions before you need them.

Keep four layers separate from the beginning:

```text
CLI parsing        (System.CommandLine, nothing else)

Orchestration      (ExecutionSession: owns the run and its lifecycle)

Observers          (membership, resources, files; each produces observations)

Parsing / readers  (pure functions over /proc, cgroup files, strace text; unit-testable)
```

---

## C# Learning Track

```text
File APIs + records
        ↓
robust parsing
        ↓
async/await + CancellationToken + Task.WhenAll
        ↓
System.Diagnostics.Process in depth
        ↓
one interface, two implementations (membership: polling / cgroup)
        ↓
PeriodicTimer / IAsyncEnumerable<T>
        ↓
System.Text.Json + schema versioning
        ↓
Span<T> / allocation awareness  (only where parsing is genuinely hot)
        ↓
LibraryImport / P/Invoke  (when signals need it)
        ↓
SafeHandle + native resource ownership  (sandbox track)
        ↓
NativeAOT + shipping a Linux-native CLI  (someday)
```

**Rule:** do not use an advanced C# feature just because it is impressive. First solve the problem simply.

---

# PART A: v0.1, The Execution Analyzer (committed scope)

## Current State

Phases 1 and 2 are complete. Keep their `inspect` command because it is useful for debugging, but from Phase 3 onward their main role is reusable internals for executions ReproBox itself launches.

---

## Phase 1: Process Inspection (Completed)

**Project role:** reusable process-observation primitive.

```bash
reprobox inspect <PID>
```

Reads `/proc/<pid>/status`, `cmdline`, `exe`, and `cwd`. Displays PID, PPID, name, command line, executable, working directory, user, thread count, and memory usage.

Lessons already learned:

- `cmdline` is NUL-separated.
- Processes can disappear while you read `/proc`, so missing data must be normal, not exceptional.
- `/proc` is a live kernel interface, not a directory of static files.

**Checkpoint:** PID vs PPID, process vs thread, virtual memory, why `/proc` exists, what happens when a shell launches a program.

---

## Phase 2: File Descriptors (Completed)

**Project role:** reusable descriptor observation for files, pipes, terminals, and later socket attribution.

```bash
reprobox inspect <PID> --fds
```

Enumerates `/proc/<pid>/fd/` and resolves each entry (terminals, regular files, pipes, `socket:[inode]`).

Lesson: observing another process's descriptor is not the same as owning a native handle. Never close what you do not own.

**Checkpoint:** stdin/stdout/stderr, what a file descriptor is, why sockets are file descriptors, how shell redirection works.

---

## Phase 3: Execution Core and Runner

**Project goal:** make ReproBox own a workload from start to finish. This is the spine every later phase attaches to. Keep it narrow: launch one root process correctly, capture its result faithfully, and establish the session/result model before adding monitoring.

```bash
reprobox run command args...
```

Record: root PID, working directory, environment snapshot, start time, end time, exit code, stdout, stderr, and a run status.

### Build in this order

1. **Environment check (new).** Detect cgroup v2, check whether your user has a delegated cgroup subtree, and check that `systemd-run --user` works. Record the result in `LEARNING.md`. Fail loudly on day one, not in Phase 4.
2. **Faithful output capture.** Read stdout and stderr as complete streams, not line by line, so ReproBox never adds or removes newlines. A reproducibility tool that alters the output it records is broken at the root.
3. **Concurrent waiting.** Output reads and process exit progress together so a chatty stderr cannot block stdout.
4. **Honest exit codes.** Command not found is 127, found but not executable is 126. A failure to launch must never be confused with the program failing.
5. **Cancellation done right.** Ctrl+C stops the workload, waits for exit, still collects whatever output existed, and marks the run as cancelled through a status, not a faked exit code.
6. **A thin session object.** It owns the run: ID, command, arguments, working directory, environment snapshot, status (created, running, exited, failed, cancelled). The result is a separate immutable record of what came out.
7. **Working directory option** so runs are reproducible from anywhere.
8. **Launch decision (Gate G2, pulled forward).** Decide *how* the workload will later be launched into a cgroup (options in Phase 4b). You do not implement cgroups here; you just make sure the launch code is a single seam you can change.

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

**Done when:** all experiments behave sensibly, output is byte-faithful, the session/result split exists, the environment check runs, and the launch decision is written down.

---

## Phase 4: Workload Membership (Polling First, Then cgroups)

**Project goal:** know exactly which processes belong to the execution ReproBox launched, and understand *why* the first approach isn't good enough. Later phases (resources, lifecycle, cleanup) all consume this membership, so it has to be right.

This phase is two halves on purpose. 4a is quick and builds the "feel the pain" intuition. 4b is the real fix. Don't skip 4a, because without it cgroups just look like magic.

**Prerequisite from Phase 3:** the launch method decision is made and the cgroup v2 + delegation check passed.

```text
ExecutionSession
    └── root PID
          ├── child PID
          │     └── grandchild PID
          └── child PID

4a: discover this by asking "who are the children of what I know?"
4b: discover this by asking the kernel "who is inside this box?"
```

### Phase 4a: Polling Observer (timebox: 1-2 days)

Keep this small. The goal is a working-but-leaky observer you can compare against later.

**Behavior.** A background task runs alongside the workload. Every \~10ms it expands from all known processes, records anything new with a first-seen time, and sleeps. On run end, one final round, then it returns its notebook.

**Design rules**

- **Remember everything seen.** Keep expanding from all known processes so reparented orphans you already saw aren't lost.
- **Reads fail quietly.** A vanished process is normal. No errors, and never print to the console mid-run (it pollutes captured output).
- **Read expensive details once** per process, at discovery.
- **Prefer the kernel's per-thread children list**; fall back to scanning `/proc` for matching parents only if it's missing.
- **Identity is PID plus start time**, not PID alone. Record start time now, because PID reuse bites later.
- **Stop cleanly.** The observer swallows its own cancellation and hands back what it has.
- Label the output "observed, best-effort."

**Write down the known leaks (you'll need them for 4b):**

- Processes that live and die between two rounds are invisible.
- Orphans reparented before being seen escape the parent chain.
- Double-forked daemons detach from the tree entirely.

**Experiments**

```bash
reprobox run sleep 2                        # one process
reprobox run sh -c "sleep 1 & sleep 1"      # shell plus two sleeps
reprobox run sh -c "sh -c 'sleep 1'"        # nesting depth
reprobox run sh -c 'for i in $(seq 200); do /bin/true; done'   # short-lived storm
```

Then compare against `pstree` on something real (dev server, build tool) and *write down what your observer missed*. That list is the motivation for 4b.

**Done when:** the shell-with-two-sleeps case shows all three processes, and the storm case visibly undercounts.

### Phase 4b: cgroup v2 Membership (timebox: 2-4 days)

**Why this exists:** a cgroup is a kernel-maintained box. Once a process is inside, so is everything it forks, and it can't casually leave. No polling race decides who's in.

**Step 1: Understand cgroups by hand first.** Before any ReproBox code, explore manually:

- Where the cgroup v2 tree lives, and what the files in a cgroup directory mean.
- What controllers are, and why some are unavailable until a parent enables them.
- Why a process's own cgroup is visible in its `/proc` entry.
- The "no internal processes" rule: why a cgroup that has child cgroups usually can't hold processes itself.
- Delegation: why you can't just write anywhere in the tree as a normal user, and what a systemd scope gives you.

Do this with `systemd-run`, by hand, and watch the membership list of a scope while a workload runs.

**Step 2: Confirm the launch method (Gate G2).** The workload must be inside the cgroup *before* it spawns children, or fast children escape.

| Option | How | Tradeoff |
| --- | --- | --- |
| **A. Launch via a systemd user scope** | ReproBox starts the workload through `systemd-run`, which sets up the cgroup and then becomes your command, so the PID stays the same | Easiest, and delegation is handled for you. Depends on systemd. |
| **B. Create your own cgroup, move the PID in after start** | Make a child cgroup under a delegated parent, then migrate the process | Tiny race window where an early fork escapes. Document it. |
| **C. Native launch helper** | Tiny helper places itself in the cgroup, then execs | Zero race, most work. Defer unless A and B both fail you. |

Recommendation: go with A. Write the reasoning in `LEARNING.md` and move on.

**Step 3: Read membership from the cgroup**

- Find the workload's cgroup from the root PID, rather than hardcoding paths.
- Read its member list on each observer tick.
- For each member, reuse your Phase 1 reader to get name, command line, parent, start time.
- **Rebuild the tree from parent PIDs.** The cgroup says *who's inside*, not *who spawned whom*. That's still `/proc`'s job, so the two sources complement each other.
- Keep the polling observer as a **fallback** when cgroup setup is unavailable, and mark which source produced the data.

**Step 4: Use the cgroup's counters as evidence.** Even if a short-lived process dies between ticks and never shows in your list, the cgroup still *counted* it. Capture:

- Peak concurrent process count (how many existed at once, even ones you never saw).
- An "is anything still alive in here" signal (the populated flag) for clean end-of-run detection.

**What cgroups do and don't fix:**

|  | Polling | cgroup |
| --- | --- | --- |
| Who's alive *right now* | Best-effort | Exact |
| Who escaped via double-fork/daemonizing | Missed | Still inside |
| Every short-lived process ever, by name | Missed | **Still missed** (you only see them if a tick catches them) |
| Total resource cost including dead children | Lost | Counted (used in Phase 6) |

So membership and accounting become authoritative, but the process *history list* is still sampled. Label accordingly.

**Step 5: Cleanup of the cgroup itself.** The cgroup directory must be removed after the run, and it can only be removed when empty. Make cleanup idempotent: a crashed run shouldn't leave stale cgroups behind, and a second cleanup attempt must be harmless. (With option A, systemd handles most of this, but verify it.)

**Experiments**

```bash
reprobox run sh -c "sleep 1 & sleep 1"            # compare 4a vs 4b output
reprobox run sh -c "(sleep 5 &) ; sleep 1"        # orphaned/detached child
reprobox run <storm fixture>                      # peak count vs what you listed
```

Verify:

- The detached-child case: polling loses the link, cgroup membership still shows the process.
- Compare your observed list against the cgroup's member list at the same instant. Any disagreement is a bug or a race, so explain it.
- Run the same workload in a plain shell vs through ReproBox and confirm the cgroup path differs and is contained.
- Deliberately break delegation (wrong environment) and confirm ReproBox falls back with a clear message instead of crashing.

**C# Focus**

- Long-running background tasks with linked cancellation, kept from 4a.
- Hide membership behind **one interface with two implementations** (polling, cgroup). This is the first abstraction in the project that earns its place.
- Keep file reading separate from tree construction so each is testable with saved samples.
- A small lifecycle type for the cgroup (create, attach, query, clean up) with idempotent cleanup.
- Typed identity record for a process (PID plus start time) used as the dictionary key everywhere.
- Treat nullable warnings as design feedback, especially around "cgroup unavailable."

**Checkpoint**

- `fork()` vs `exec()`, and why children get reparented when a parent dies.
- Why polling can never be ground truth.
- What a cgroup is, what "no internal processes" means, and what delegation is.
- Why launching *into* the cgroup matters, and what race exists if you move a PID in afterward.
- Namespaces control what a process can *see*; cgroups control what it can *consume* and *where it belongs*.

**Done when:**

- The detached-child case is captured by cgroup membership but missed by polling.
- The run result carries a membership list tagged with its source (cgroup or polling fallback).
- Peak process count is recorded.
- Cgroup cleanup is idempotent and leaves nothing behind after normal, failed, and Ctrl+C runs.
- You can explain why the process *list* is still labeled best-effort even though membership is authoritative.

---

## Phase 5: Attach Process and FD Observations to the Execution

**Project goal:** stop treating Phases 1-2 as standalone demos. Reuse them while a session is active.

For each workload process (from Phase 4 membership), attach: PID/PPID, name, command line, executable, working directory, user, threads, and a memory snapshot. File descriptors are attached as a second, separate observation.

Decide explicitly which values are:

- **Static for a process's life** (executable, command line): read once.
- **Sampled** (memory, threads): read periodically, keep summaries.
- **Event-like** (descriptors opened and closed): snapshot, and accept that you will miss some.

Do not rescan every expensive field at maximum frequency.

### Experiments

Use a controlled sample that opens regular files, creates a pipe, opens a socket, and sleeps. Run it through `reprobox run` and verify the same information you previously inspected manually appears inside the run. Also run a workload that spawns a child, to verify observations exist for more than the root.

### C# Focus

- Reuse the existing snapshot and descriptor records instead of duplicating parser output.
- Update observations by PID (really PID plus start time) without rebuilding unrelated state.
- Make missing data normal: a short-lived descendant may be gone between discovery and inspection.
- Keep ownership clear: observing is not owning.

### Checkpoint

Why `/proc` observations are snapshots not truth, which process information changes during a run, why a PID can disappear between two reads, why descriptor data matters later for files and sockets.

---

## Phase 6: Resource Monitoring

**Project goal:** measure the cost of the **whole execution**. Output must feed the run result, not exist as a standalone monitor.

Track: wall-clock time, CPU time, resident memory (with peak), virtual memory, threads, context switches, disk reads, disk writes.

**Sources (changed in v3):**

- **Primary: the workload's cgroup counters** (CPU usage, memory current and peak, I/O bytes, process count). These include processes that died between samples, so the old undercount is gone.
- **Cross-check and per-process detail: `/proc/<pid>/stat`, `status`, `io`**, for members you observe. Context switches and virtual memory only come from here, so those remain sampled and best-effort.
- If running in the polling fallback, totals revert to summing `/proc` across the observed tree, and the report states that it undercounts.

Design rules:

- Sample while the run is alive on a steady timer, not a sleep loop that drifts.
- **Keep peaks, totals, and averages incrementally.** Do not store every sample forever.
- Read the cgroup's final counters once more at the end of the run, before cleanup removes the cgroup.
- Give every number a unit in its name or type. Ticks, pages, bytes, milliseconds, and percentages must never be bare numbers.
- Tag every figure with its source (cgroup or /proc).

### Experiments

Four controlled fixtures, compared side by side: one that burns CPU for a known time, one that allocates a known amount of memory and holds it, one that writes and reads a known amount of disk, and one that just sleeps. Also compare:

```bash
reprobox run sha256sum huge-file.iso
reprobox run sleep 10
```

Check your numbers against `top` and `/usr/bin/time -v` for the same workloads. When they disagree, find out why. Include a workload that spawns many short-lived children, and confirm the cgroup total beats a summed `/proc` total.

### C# Focus

- `PeriodicTimer`, `Stopwatch`, async loops with cancellation.
- `IAsyncEnumerable<ResourceSample>` if streaming samples genuinely helps; a simple callback or accumulator is fine if it does not.
- Value types with units instead of raw `long`s.
- Incremental statistics (peak, average) without unbounded memory.
- Parsing the cgroup's flat key-value files separately from the `/proc` parsers.

### Checkpoint

CPU time vs wall time, RSS vs virtual memory, blocking I/O, context switches, peak vs current memory, what a cgroup counts that `/proc` per-process sums cannot.

**Done when:** each fixture produces numbers that are believable against an independent tool, and the remaining `/proc`-only limitations are documented. (Gate G1 is retired: the many-short-children test replaces it.)

---

## Phase 7: Lifecycle Control: Timeouts, Signals, Cleanup

**Project goal:** make stopping a workload a first-class part of the session lifecycle. ReproBox owns the workload, so it must be able to stop it, escalate, and clean up the whole tree.

```bash
reprobox run --timeout 30s command
```

Behavior to implement:

- **Graceful first:** on timeout or interrupt, send a polite termination signal to the workload.
- **Escalate:** after a grace period, force-kill whatever is left.
- **Report honestly:** the result distinguishes "exited on its own," "stopped by ReproBox after timeout," and "interrupted by the user," using status, not exit-code tricks.
- **Leave nothing behind:** after the run, verify the cgroup is empty (the populated flag is false) and the cgroup is removed.

**What changed in v3:** the cgroup can kill everything inside it in one operation, so the force-kill step no longer depends on perfectly tracking every PID or process group. Graceful-then-escalate is still the lesson: the polite signal goes to the processes, and the cgroup kill is the backstop. Process groups matter less, but learn them anyway: a polite signal still has to reach the whole tree, and the terminal's Ctrl+C is delivered to a process group.

### Experiments

- A sample that handles the termination signal, records that it received it, and exits cleanly. Compare with a forced kill.
- A sample that ignores the polite signal, to exercise the escalation path.
- A sample that spawns children (and a detached grandchild), then stop it and check nothing survives.
- Test what happens to children when the parent exits first.
- Confirm a double-forked daemon is still killed by the cgroup backstop.

### C# Focus

- Modern source-generated P/Invoke (`[LibraryImport]`) for small libc calls such as sending a signal.
- A signal enum instead of scattered integer constants.
- Translating native errors into meaningful .NET errors.
- Keep all native calls behind one tiny boundary type so the rest stays ordinary managed C#.
- Reuse the idempotent cgroup lifecycle type from Phase 4b for the kill-and-cleanup path.

### Gate G2 follow-up

If you chose launch option A (systemd scope) in Phase 3, check here whether `System.Diagnostics.Process` still blocks anything you need, such as putting the child in its own process group. If it fights you, add a small wrapper or native launch helper now. Decide here, not later; the sandbox track needs the same capability.

### Checkpoint

Why a forced kill cannot be handled, graceful shutdown, why Ctrl+C works, process groups, what happens to children when a parent exits, why a cgroup kill is a stronger backstop than signalling PIDs.

**Done when:** timeout, interrupt, and natural exit produce three distinct statuses, and no process or cgroup survives any of them.

---

## Phase 8: Filesystem Observation

**Project goal:** answer "what files did this execution reference, read, create, or modify?" and feed that into the report.

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
- Make sure tracing composes with the cgroup launch: the traced command still has to start inside the workload's cgroup, so membership and resource numbers stay correct.

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

## Phase 9: Versioned Execution Report

**Project goal:** turn one observed execution into a durable, machine-readable artifact, plus a readable terminal summary. This is where the data model gets decided, which is why it comes before any isolation work.

```bash
reprobox run --report run.json command
```

Store at least: schema version, command, arguments, working directory, environment snapshot, timing, exit status and run status, **membership (with its source: cgroup or polling fallback)**, observed processes, resource summary (each figure tagged with its source), file observations.

Rules:

- **Explicit schema version from day one.** Treat the report shape as an API that must stay compatible.
- **Do not serialize internal objects automatically.** Define the persisted shape deliberately; keep internal models separate once they diverge.
- **Label provenance.** Three tiers: *kernel-authoritative* (cgroup membership and counters), *observed/best-effort* (`/proc` and `strace` data), and *inferred* (a separate, explicitly marked section, later).
- **Partial reports are valid.** A crashed or interrupted run still finalizes a report with whatever was collected, plus the failure state.
- Keep the report immutable after finalization.
- Record the environment capabilities (cgroup v2, delegation, fallback used) so a reader knows how trustworthy the numbers are.

### Experiments

- Run the same deterministic fixture twice and diff the reports. Decide which differences are noise (timestamps, PIDs) and which are signal.
- Run `sleep 2` and verify irrelevant sections stay small instead of filling with noise.
- Run a crashing process and verify a complete report is still written.
- Interrupt a workload and confirm the report separates a normal exit from a ReproBox-initiated stop.
- Force the polling fallback and confirm the report says so.

### C# Focus

- `System.Text.Json` with an explicit version field and deliberate property naming.
- Separating domain models from persisted DTOs.
- Immutable records for finalized data.
- Streaming for large sections instead of building enormous strings.

### Checkpoint

Snapshot/event data vs final summary data, internal model vs file format, why schema versioning matters, which report fields are facts and which are future inferences.

---

## Milestone 1: ReproBox v0.1 Release (Execution Analyzer)

At this point `reprobox run` is the unifying command and everything is a property of one execution.

```bash
reprobox run --report run.json --trace-files npm test
```

reports:

```text
exit status and run status

workload membership (cgroup-authoritative) and observed process tree

CPU, memory, and I/O for the whole run, including short-lived children

files read, created, and modified

labels on every source: kernel-authoritative vs best-effort
```

### Release checklist

- README with a short demo, a requirements section (Linux, cgroup v2, delegation), and an honest limitations section
- Sample workloads under `samples/` used as test fixtures
- Parser tests using saved `/proc`, cgroup-file, and `strace` samples (no live PID required)
- `LEARNING.md` entries for every phase (Linux concept and C# concept)
- Tagged v0.1

### Gate G3

Do you still want to keep going? If yes, pick **one** track below. If no, you have a complete, finished project. That is already a strong result.

---

# PART B: Optional Enrichments (v0.2)

Small additions to the same run report. Do these only if they help you, not to fill space.

## Phase 10: Syscall Summary

**Project goal:** add syscall evidence that is useful for diagnosis, not just a pretty table.

```bash
reprobox run --trace-syscalls command
```

Use `strace`'s summary mode, parse it into your own structured model (counts and time per syscall), and attach it to the report.

**Experiments:** write "Hello World" in C, C#, and Python and compare the syscall patterns. Find out why a language runtime makes hundreds of calls before your code runs.

**C# Focus:** aggregation model separate from parsing and from formatting; LINQ for sorting and projection once the raw parser is correct; benchmark only if you have a real performance question.

**Checkpoint:** syscall vs normal function call, userspace vs kernel space, libc vs kernel, why `Console.WriteLine()` eventually causes syscalls.

## Phase 11: Network Sockets

**Project goal:** attach socket state to the workload by reusing the descriptor data, not as a separate networking toy.

Read the kernel's TCP/UDP socket tables, match socket inode numbers to the descriptors you already observe, and show state, local address, remote address, and owning process.

Important: this shows socket **state**, not a history of connections. Events like connect/accept/bind need syscall tracing.

**Experiments:** a local client/server fixture so expected LISTEN and ESTABLISHED states are deterministic and never depend on the public internet. Then compare with `ss`.

**C# Focus:** `IPAddress`, `BinaryPrimitives`, and hexadecimal parsing; implement the endianness conversion yourself once before using a helper; dictionary/set joins between inodes and descriptors.

**Checkpoint:** TCP vs UDP, listening vs connected, localhost, ports, DNS, client/server, why socket tables are snapshots.

---

# PART C: Pick ONE Track

Make the choice at Gate G3. Gate G4 after the first phase of the chosen track lets you change your mind.

## Track S: Sandbox

Goal: run the same session pipeline inside a restricted environment. Isolation wraps the existing execution path; it must not become a second architecture.

### S1: cgroup Limits (shrunk in v3)

Membership, accounting, and cleanup already shipped in Phases 4, 6, and 7. This phase is only about **limits**:

```bash
reprobox run --memory 512M command
reprobox run --cpu 0.5 command
```

Learn the control files for memory limits and CPU quota, and which controllers must be enabled in the delegated subtree. Understand what happens at the limit: memory pressure, throttling, and the out-of-memory kill, and how each shows up in the report.

**Experiments:** a memory-eater fixture run under a small limit; observe exactly what happens when it exceeds it. A CPU burner under a half-core quota; compare wall time to CPU time.

**C# focus:** typed limit values instead of raw strings; extend the existing cgroup lifecycle type rather than creating a new one.

**Checkpoint:** namespaces control what a process can **see**; cgroups control what it can **consume**.

Whether limits ship in v0.1 or after is your call at the timebox. Default: after, to protect the date.

### S2: Namespaces

Experiment manually with `unshare` and `nsenter` first. Learn them one at a time: hostname, process IDs, mounts, network, users. Start by having ReproBox invoke the existing tools; only write a native helper if you must.

**C# focus:** Linux-only boundaries, keeping platform-specific code isolated, `SafeHandle` if namespace descriptors become long-lived.

**Checkpoint:** containers are isolated processes, not miniature virtual machines; why PID namespaces behave differently from changing a hostname.

### S3: Filesystem Isolation

Manual experiments first: mount namespaces, bind mounts, tmpfs, `chroot`, `pivot_root`, OverlayFS. Target a base layer plus a writable layer presented as one root.

**C# focus:** deterministic cleanup, explicit path validation, testing the mount plan separately from executing it.

### S4: Network Isolation

Start with `--no-network` using a fresh network namespace. Then experiment with virtual interface pairs, addresses, and routing using the standard `ip` tools before considering raw netlink.

**C# focus:** a reusable external-command runner, typed network configuration steps, `System.Net` types instead of raw strings.

## Track R: Record and Replay

Goal: use the versioned report to reconstruct a sufficiently similar environment and rerun the workload. Aim for "similar environment, similar behavior," never bit-for-bit reproduction.

### R1: Record

Capture command, arguments, working directory, environment, input file identities (hashes, not copies, at first), and the observation report as a recorded run with an ID.

**C# focus:** streaming I/O, SHA-256 hashing, content-addressed storage concepts, schema versioning and migration of saved runs.

### R2: Replay

Rebuild the environment from a recorded run and execute it again through the same session pipeline.

### R3: Compare

Diff two reports (original vs replay) and explain what differs and why: timing noise, different PIDs, missing files, different environment. This is where the report design pays off.

---

# PART D: Someday List

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

✓ exact workload membership via cgroups (with an honest polling fallback)

✓ measure CPU, memory, and I/O for the whole run, short-lived children included

✓ stop workloads gracefully and clean up with nothing left behind

✓ trace file activity

✓ export a versioned execution report
```

Everything after Milestone 1 is a bonus.

---

## Requirements and Known Limitations (for the README)

**Requires:** Linux, cgroup v2, and a delegated cgroup subtree (systemd user scope recommended).

**Kernel-authoritative (with cgroups):** workload membership at sample time, whole-run CPU, memory, I/O, and peak process count.

**Still best-effort:**

- The process *history list* is sampled; short-lived processes can be counted by the cgroup without ever being named.
- Parent/child relationships come from `/proc` and can be missing for processes that vanish between reads.
- Context switches and virtual memory are `/proc`-only and sampled.
- `strace` adds overhead and slows the workload.
- `/proc` data is a snapshot of a live kernel interface, not durable truth.
- In the polling fallback (no cgroups), membership and resource totals can undercount, and the report says so.

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

Do not ask: "Implement Phase 7 for me." Prefer: "I expected the child process to show up here but it doesn't. What assumption am I getting wrong?"

---

## Existing Tools to Learn First

| ReproBox feature | Explore first |
| --- | --- |
| Processes | `ps`, `/proc` |
| Process tree | `pstree` |
| Workload membership (cgroups) | `systemd-run --user --scope`, `systemd-cgls`, `/sys/fs/cgroup`, `/proc/<pid>/cgroup` |
| File descriptors | `lsof`, `/proc/*/fd` |
| Resource usage | `top`, `/usr/bin/time -v`, `systemd-cgtop` |
| Signals | `kill` |
| Syscalls and files | `strace` |
| Network | `ss` |
| Memory | `pmap`, `/proc/*/maps` |
| Namespaces | `unshare`, `nsenter` |
| Mounts | `mount`, `findmnt` |
| Capabilities | `capsh`, `getcap` |
| eBPF | `bpftrace` |

The goal is not to rewrite these tools. It is to understand the primitives underneath them and combine them into something useful. ReproBox exists to learn Linux by building, not to replace strace, bubblewrap, or Docker.

---

## Testing Strategy

Keep small deterministic sample programs under `samples/`. They are **test fixtures for ReproBox**, not tutorials.

| Fixture | Used by | Behavior |
| --- | --- | --- |
| Process sample | Phases 4, 5, 7 | Spawns a child and grandchild, then waits |
| Detached sample | Phases 4b, 7 | Double-forks a child that outlives its parent (proves cgroup membership) |
| Storm sample | Phases 4, 6 | Spawns hundreds of short-lived children (proves the polling leak and the cgroup fix) |
| Descriptor sample | Phases 5, 11 | Opens regular files, a pipe, and a socket, then sleeps |
| CPU sample | Phase 6 | Burns CPU for a known duration |
| Memory sample | Phase 6, S1 | Allocates and holds a known amount |
| Disk sample | Phase 6 | Writes and reads a known amount |
| Sleep sample | Phase 6 | Does nothing; baseline |
| Signal sample | Phase 7 | Handles the polite termination signal and records it |
| Stubborn sample | Phase 7 | Ignores the polite signal to test escalation |
| File sample | Phase 8 | Reads one input, writes one output, stats another path |
| Network sample | Phase 11 | Local client/server pair with deterministic socket states |
| Crash sample | Phases 3, 9 | Exits non-zero with output on stderr |

Parser tests use saved `/proc`, cgroup-file, and `strace` samples so most tests never need a live process.

---

## Immediate Next Work

You are inside **Phase 3**. In order:

1. Make output capture byte-faithful and concurrent.
2. Add honest exit codes for not-found and cannot-execute.
3. Add the session object and the status/result split.
4. Run the cgroup v2 + delegation environment check and write the result down.
5. Write down the launch decision (Gate G2).
6. Build the process, detached, and storm fixtures.
7. Move to Phase 4a (polling), then 4b (cgroups).
8. Pick your v0.1 date and write it at the top of `LEARNING.md`.

---

## What Changed from v2

- **cgroup v2 moved into Phase 4.** Phase 4 is now 4a (polling, to feel the leaks) and 4b (cgroup membership as the source of truth).
- **Phase 3** gained the environment check and the launch decision (Gate G2 pulled forward).
- **Phase 6** now sources totals from cgroup counters, keeps `/proc` as a cross-check, and **Gate G1 is retired**.
- **Phase 7** uses the cgroup as the kill backstop; graceful-then-escalate stays as the lesson.
- **Phases 8 and 9** compose tracing with the cgroup launch and add provenance tiers and environment capabilities to the report.
- **Track S1** shrank to limits only.
- **Scope rule 4, requirements, limitations, tools table, and fixtures** updated to match, including new detached and storm fixtures.
