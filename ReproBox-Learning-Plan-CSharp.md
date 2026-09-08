# ReproBox — Linux + C# Learning Plan

Goal:

> Build a Linux execution analysis and sandboxing tool in C# that can inspect processes, trace their behavior, isolate them, and eventually reproduce workloads.

The learning progression is:

```text
/proc
  ↓
processes
  ↓
file descriptors
  ↓
process trees
  ↓
process execution
  ↓
signals
  ↓
resource usage
  ↓
syscalls/files/network
  ↓
namespaces
  ↓
cgroups
  ↓
security
  ↓
eBPF
  ↓
dependency detection
  ↓
replay
  ↓
sandbox generation
```

## Project Baseline

Start deliberately small:

```text
ReproBox/
├── src/ReproBox.Cli/
├── tests/ReproBox.Tests/
└── LEARNING.md
```

Use **.NET 10 / `net10.0`** and enable nullable reference types. Keep ReproBox primarily C#; use a tiny C/Rust helper only if a Linux primitive becomes awkward or unsafe to own from managed code.

Do not add abstractions before you need them. Start with ordinary managed APIs, then cross into native Linux APIs only when the phase requires it.

## C# Learning Track

The C# progression should happen alongside the Linux progression:

```text
File APIs + records
        ↓
robust parsing
        ↓
Span<T> / allocation awareness
        ↓
async/await + CancellationToken
        ↓
IAsyncEnumerable<T> / Channels<T>
        ↓
System.Net + binary parsing
        ↓
LibraryImport / P/Invoke
        ↓
SafeHandle + native resource ownership
        ↓
NativeAOT + shipping a Linux-native CLI
```

**Rule:** do not use an advanced C# feature just because it is impressive. First solve the problem simply; introduce the feature when you can explain what problem it solves.

---

## Phase 1 — Process Inspection

Build:

```bash
reprobox inspect <PID>
```

Read:

```text
/proc/<pid>/status
/proc/<pid>/cmdline
/proc/<pid>/exe
/proc/<pid>/cwd
```

Display:

```text
PID
PPID
process name
command line
executable
working directory
user
thread count
memory usage
```

Important:

* `cmdline` is NUL-separated.

* Processes may disappear while you are reading `/proc`, so handle missing files gracefully.

* Treat `/proc` as a live kernel interface, not a normal directory full of static files.

### C# Focus

- Start with `File.ReadAllText`, `File.ReadAllBytes`, `Directory`, and `FileInfo` so the Linux behavior stays visible.
- Model one observation as a small immutable `record` such as `ProcessSnapshot`.
- Parse numeric fields with `TryParse` and model missing data explicitly instead of treating every disappearing `/proc` file as exceptional.
- Once the parser is correct, rewrite the hot parsing paths with `ReadOnlySpan<char>` / `ReadOnlySpan<byte>` and compare readability and allocations.
- Unit-test parsers using saved `/proc` samples rather than requiring a live PID in every test.

### Checkpoint

Be able to explain:

* PID vs PPID

* process vs thread

* virtual memory

* why `/proc` exists

* what happens when a shell launches a program

---

## Phase 2 — File Descriptors

Add:

```bash
reprobox inspect <PID> --fds
```

Enumerate:

```text
/proc/<pid>/fd/
```

Resolve entries such as:

```text
0 → /dev/pts/2
1 → /dev/pts/2
2 → /dev/pts/2
3 → /home/user/data.db
4 → socket:[382911]
```

Write a small test program that opens a file and sleeps for 30 seconds.

Inspect it while it is running.

Also experiment with:

```bash
echo hello > file.txt
```

### C# Focus

- Use `Directory.EnumerateFileSystemEntries` and `File.ResolveLinkTarget` rather than loading everything eagerly.
- Represent descriptors with a `FileDescriptor` record and a small `enum` for file/socket/pipe/other.
- Practice iterator methods with `yield return` so descriptor discovery can stay lazy.
- Learn the difference between **observing another process's fd** and **owning a native handle yourself**; do not close resources you do not own.

### Checkpoint

Understand:

* stdin

* stdout

* stderr

* what a file descriptor is

* why sockets are file descriptors

* how shell redirection works

---

## Phase 3 — Process Trees

Build:

```bash
reprobox tree <PID>
```

Construct parent/child relationships using PPIDs from `/proc`.

Example:

```text
node
├── sh
│   └── git
├── esbuild
└── worker
```

Experiment with:

```bash
npm run dev
docker compose up
```

Compare the process trees.

### C# Focus

- Build the tree with `Dictionary<int, ProcessNode>` and explicit parent/child links.
- Implement traversal once imperatively, then compare a LINQ-based version so you understand what LINQ is hiding.
- Practice recursion vs an explicit `Stack<T>` for tree traversal.
- Keep parsing separate from tree construction so each part can be tested independently.

### Checkpoint

Understand:

```text
fork()
exec()
```

and how they differ conceptually.

---

## Phase 4 — Process Runner

Build:

```bash
reprobox run command args...
```

Use `System.Diagnostics.Process`.

Record:

```text
PID
start time
end time
exit code
stdout
stderr
direct child
observed descendants
```

Initially, descendant tracking may be best-effort because short-lived processes can disappear between `/proc` scans.

Test with:

```bash
reprobox run sleep 10
reprobox run ls /
reprobox run python crash.py
```

### C# Focus

- Learn `System.Diagnostics.Process` and `ProcessStartInfo` properly: `UseShellExecute`, argument handling, environment, working directory, and redirected streams.
- Capture stdout/stderr asynchronously and use `WaitForExitAsync`.
- Use `Task.WhenAll` where stdout, stderr, resource sampling, and process completion must progress concurrently.
- Thread a `CancellationToken` through the runner instead of inventing custom cancellation flags.
- Return a typed `RunResult` record rather than passing loosely related values around.

### Checkpoint

Be able to explain:

```text
shell
  ↓
ReproBox
  ↓
child process
  ↓
kernel
```

---

## Phase 5 — Signals

Add:

```bash
reprobox stop <run>
reprobox interrupt <run>
reprobox kill <run>
```

Map these roughly to:

```text
stop       → SIGTERM
interrupt  → SIGINT
kill       → SIGKILL
```

Use P/Invoke for `kill(pid, signal)`.

Write a small program that handles `SIGTERM`.

Compare it with `SIGKILL`.

Later, learn process groups so ReproBox can signal an entire workload rather than only one PID.

### C# Focus

- Prefer modern source-generated P/Invoke with `[LibraryImport]` for small libc calls such as `kill`.
- Represent signals with an enum rather than scattering integer constants through the code.
- Learn `SetLastError`/`Marshal.GetLastPInvokeError()` and translate native failures into meaningful .NET errors.
- Keep native interop behind a tiny `LinuxNative` boundary so most of ReproBox remains ordinary managed C#.

### Checkpoint

Understand:

* why SIGKILL cannot be handled

* graceful shutdown

* why Ctrl+C works

* process groups

* what can happen to children when a parent exits

---

## Phase 6 — Resource Monitoring

Track:

```text
CPU time
wall-clock time
RSS
virtual memory
threads
context switches
disk reads
disk writes
```

Read data from:

```text
/proc/<pid>/stat
/proc/<pid>/status
/proc/<pid>/io
```

Poll while the process is alive.

Compare:

```bash
reprobox run sha256sum huge-file.iso
reprobox run sleep 10
```

### C# Focus

- Use `PeriodicTimer`, `Stopwatch`, async loops, and `CancellationToken` for sampling.
- Expose samples as `IAsyncEnumerable<ResourceSample>` so monitoring is naturally streaming instead of returning one giant list.
- Learn value types and units: avoid mixing ticks, bytes, pages, milliseconds, and percentages as unlabelled `long`s.
- Calculate peaks/averages incrementally instead of storing every sample forever.

### Checkpoint

Understand:

* CPU time vs wall time

* RSS vs virtual memory

* blocking I/O

* context switches

* peak memory vs current memory

---

## Phase 7 — Filesystem Observation

Build:

```bash
reprobox trace-files command
```

Start by using `strace`.

Example:

```bash
strace -f -e trace=%file -o trace.log command
```

Parse the trace yourself.

Do not simply dump `strace` output.

Build your own dependency model.

Example:

```text
Files referenced:
READ/OPEN
  ./config.json
  /usr/lib/libssl.so
CREATED/MODIFIED
  ./logs/server.log
```

Later experiment with:

```text
%file
%desc
%memory
-y
-yy
```

Do not assume `trace=%file` means every `read()` or `write()` is captured. It mainly traces syscalls involving file paths.

### C# Focus

- Treat `strace` as an external producer and parse its output into typed `TraceEvent` records.
- Start with a simple parser; then compare regex parsing with a `ReadOnlySpan<char>`/state-machine parser.
- Stream events instead of reading a giant trace file into memory.
- If parsing and aggregation happen concurrently, introduce `Channel<TraceEvent>` and understand the backpressure problem it solves.

### Checkpoint

Understand:

* `openat`

* `stat`

* `read`

* `write`

* `mmap`

* shared libraries

* why a process can depend on a file without explicitly calling `read()` on it

---

## Phase 8 — System Call Analysis

Build:

```bash
reprobox syscalls command
```

Use:

```bash
strace -f -c command
```

Parse the result into your own format:

```text
openat     382
read       811
write      217
mmap        74
futex      928
clone        8
connect      3
```

Write Hello World in:

```text
C
C#
Python
```

Compare the syscall patterns.

### C# Focus

- Build a small aggregation model such as `Dictionary<string, SyscallStats>` instead of keeping presentation logic mixed with parsing.
- Practice sorting/projection with LINQ after the raw parser is correct.
- Keep raw trace events, aggregated statistics, and terminal formatting as separate types/layers.
- Use BenchmarkDotNet only if you have a real parsing/performance question; do not benchmark for decoration.

### Checkpoint

Understand:

* syscall vs normal function call

* userspace vs kernel space

* libc vs kernel

* why `Console.WriteLine()` eventually causes syscalls

---

## Phase 9 — Networking

Inspect:

```text
/proc/<pid>/net/tcp
/proc/<pid>/net/tcp6
/proc/<pid>/net/udp
/proc/<pid>/net/udp6
```

Map socket inode numbers back to:

```text
/proc/<pid>/fd/*
```

Display:

```text
LISTEN
ESTABLISHED
UDP sockets
local address
remote address
port
```

Important:

This shows socket state, not necessarily historical connection events.

Later use syscall tracing or eBPF for events like:

```text
connect()
accept()
bind()
```

Experiments:

```bash
curl example.com
python -m http.server 8000
```

### C# Focus

- Use `IPAddress`, `BinaryPrimitives`, and span-based hexadecimal parsing to decode `/proc` socket tables.
- Learn endianness by implementing the address conversion yourself before hiding it behind a helper.
- Use `Dictionary`/`HashSet` to join socket inodes to process descriptors efficiently.
- Write tiny test servers/clients with `System.Net.Sockets` so you control the socket states ReproBox is expected to observe.

### Checkpoint

Understand:

* sockets

* TCP vs UDP

* listening vs connected

* localhost

* ports

* DNS

* client/server

---

## Milestone 1 — ReproBox v0.1

At this point:

```bash
reprobox run npm test
```

should report roughly:

```text
exit status
process tree
CPU usage
memory usage
filesystem activity
socket activity
syscall summary
```

Release this before starting isolation.

This alone is already a useful tool.

---

## Phase 10 — Namespaces

Before implementing anything, experiment manually with:

```bash
unshare
nsenter
```

Learn namespaces one at a time:

```text
UTS
PID
mount
network
user
```

Do not immediately build all namespace logic directly inside the main C# process.

Start by making ReproBox invoke existing namespace tools.

Later, if needed, create a tiny native helper such as:

```text
reprobox-init
```

written in C or Rust.

ReproBox itself can remain C#.

### C# Focus

- Initially invoke `unshare`/`nsenter` with `Process`; keep the managed orchestration code simple while learning namespace semantics.
- Guard Linux-only code with `OperatingSystem.IsLinux()` and keep platform-specific code isolated.
- If you later call namespace APIs directly, use `[LibraryImport]` and learn native struct/constants carefully rather than copying signatures blindly.
- If namespace file descriptors become long-lived resources, introduce `SafeHandle` instead of managing raw `int` handles ad hoc.

### Checkpoint

Understand:

> Containers are isolated processes, not miniature virtual machines.

Also understand why PID namespaces behave differently from something simple like changing the hostname.

---

## Phase 11 — Filesystem Isolation

Build toward:

```bash
reprobox run --root ./sandbox command
```

Experiment manually with:

```text
mount namespaces
bind mounts
tmpfs
chroot
pivot_root
OverlayFS
```

Target architecture:

```text
base filesystem
      +
writable layer
      ↓
isolated filesystem
```

### C# Focus

- Practice deterministic cleanup with `try/finally`, `IDisposable`, and eventually `SafeHandle` for resources ReproBox actually owns.
- Keep path normalization/validation explicit; filesystem isolation code should not rely on string concatenation scattered through the project.
- If you P/Invoke `mount`/`umount2`, keep the unsafe/native boundary tiny and test the high-level mount plan separately from execution.

### Checkpoint

Understand how container filesystems work conceptually.

---

## Phase 12 — cgroups v2

Add:

```bash
reprobox run --memory 512M command
reprobox run --cpu 0.5 command
```

Learn:

```text
/sys/fs/cgroup/
memory.max
cpu.max
cgroup.procs
```

Do not assume your process can write anywhere under `/sys/fs/cgroup`.

Learn about cgroup delegation.

Write a memory-eater test program:

```bash
reprobox run --memory 100M ./memory-eater
```

Observe what happens when the process exceeds the limit.

### C# Focus

- Model limits as typed values (`MemoryLimit`, `CpuQuota`) instead of raw strings passed directly into cgroup files.
- Build a small cgroup lifecycle object that creates, configures, attaches, and cleans up a delegated cgroup.
- Use async file APIs only where they actually help; tiny control-file writes do not need artificial complexity.
- Make cleanup idempotent so failed runs do not leave stale cgroups behind.

### Checkpoint

Understand:

> Namespaces control what a process can see.

> cgroups control what a process can consume.

---

## Phase 13 — Network Isolation

Build:

```bash
reprobox run --no-network command
```

Start with a network namespace.

Then experiment with:

```text
veth pairs
IP addresses
routing
```

Initially use:

```bash
ip
ip link
ip netns
```

rather than writing raw netlink code.

Later consider:

```bash
reprobox run --allow example.com:443 command
```

### C# Focus

- Build a reusable external-command runner for `ip` operations rather than duplicating `ProcessStartInfo` logic.
- Represent network setup as typed steps/configuration before executing shell commands.
- Use `System.Net` types for addresses/prefixes instead of passing unvalidated strings everywhere.
- If a native helper is introduced, consider a Unix-domain socket for structured IPC between C# and the helper.

---

## Phase 14 — Linux Security

Study these separately:

```text
Linux capabilities
no_new_privs
seccomp
Landlock
```

Do not add them all at once.

First:

```text
inspect capabilities
```

Then:

```text
drop capabilities
```

Then add seccomp restrictions.

Example:

```bash
reprobox run --deny-syscall execve program
```

Observe what breaks.

If using libseccomp from C#, prefer non-variadic APIs that are easier to P/Invoke.

### C# Focus

- This is the main native-interop phase: learn `[StructLayout]`, `[Flags]` enums, `[LibraryImport]`, `SafeHandle`, and correct ownership rules.
- Prefer fixed/non-variadic libseccomp APIs that map safely to P/Invoke.
- Keep the **policy compiler** pure C# and unit-testable; only the final enforcement adapter should touch native APIs.
- Avoid `unsafe` code unless a specific API requires it. If you use it, document exactly why managed code was insufficient.

### Checkpoint

Understand the difference between:

```text
isolation
resource limiting
privilege reduction
syscall filtering
```

---

## Phase 15 — eBPF

Do not start here.

Only use eBPF once you already understand the behavior you want to observe.

Start with `bpftrace`.

Trace:

```text
execve
openat
connect
```

Conceptually:

```text
userspace loader
     ↓
eBPF program
     ↓
kernel verifier
     ↓
kernel event
     ↓
eBPF executes
     ↓
event returned to userspace
```

Replace earlier tracing mechanisms only where eBPF genuinely gives you something better.

Examples:

```text
lower overhead
system-wide observation
better event visibility
```

### C# Focus

- Consume `bpftrace` output as an async event stream using `IAsyncEnumerable<T>` or `Channel<T>`.
- Design for bursts: kernel events may arrive faster than the UI/reporting side can process them, so understand buffering and backpressure.
- If you later replace `bpftrace` with a native eBPF loader, keep the loader behind a narrow IPC/native boundary and preserve the same C# event model.

---

## Phase 16 — Dependency Detection

Build:

```bash
reprobox learn ./server
```

Aggregate everything collected so far.

Example:

```text
FILES
Read:
  ./config/*
  /usr/lib/**
Write:
  ./logs/*
ENVIRONMENT SUPPLIED
  DATABASE_URL
  REDIS_URL
NETWORK
  postgres:5432
  api.example.com:443
RESOURCES
  Peak memory: 380 MB
  CPU: ~1 core
```

Important:

Do not claim an environment variable was actually used just because it existed in the environment.

Call it:

```text
Environment supplied
```

unless you later add more precise instrumentation.

### C# Focus

- This is a domain-modelling phase: represent **observed facts** separately from **inferred requirements**.
- Use `HashSet<T>` for deduplication and explicit comparers for normalized paths/endpoints.
- Use LINQ for aggregation only after you can explain the underlying set/group operations.
- Keep the dependency model independent of where an observation came from (`/proc`, `strace`, eBPF, etc.).

---

## Phase 17 — Record and Replay

Build:

```bash
reprobox record npm test
reprobox replay run-821
```

Store:

```text
command
arguments
working directory
environment
filesystem dependencies
network policy
resource limits
execution metadata
```

Try reproducing the workload on another machine or sandbox.

Do not aim for perfect bit-for-bit reproducibility initially.

Aim for:

> similar environment → similar behavior

### C# Focus

- Use `System.Text.Json` with an explicit schema version for recorded runs.
- Learn streaming I/O, SHA-256 hashing, and content-addressed storage concepts instead of copying whole directory trees blindly.
- Make replay data structures immutable where practical so a recorded run is not accidentally mutated during execution.
- Treat compatibility/version migration of saved run files as a real API design problem.

---

## Phase 18 — Automatic Sandbox Generation

Build:

```bash
reprobox learn ./server
reprobox generate-policy
```

Generate:

```yaml
filesystem:
  read:
    - ./config/**
    - /usr/lib/**
  write:
    - ./logs/**
network:
  allow:
    - postgres:5432
resources:
  memory: 512MB
```

Then:

```bash
reprobox run --policy generated.yaml ./server
```

Use:

```text
namespaces
cgroups
capabilities
seccomp
filesystem restrictions
```

to enforce the policy.

Important:

Generated policies are:

> observed policy candidates

not guaranteed-complete security policies.

A different code path may require files, syscalls, or network access that the learning run never observed.

### C# Focus

- Define the policy as typed C# domain objects first; YAML/JSON is only a serialization format.
- Validate policies before enforcement and return structured validation errors rather than throwing on every bad field.
- If you use YAML, keep the serializer behind an adapter so the policy model is not coupled to one package.
- Finish by trying a trimmed/NativeAOT publish and fix reflection/dynamic-code assumptions that prevent a small Linux-native binary.

---

## C# Checkpoint by the Portfolio Release

By the time you stop around Phases 1–12, you should be comfortable explaining and using:

```text
records / immutable models
File + Directory APIs
robust parsing
Span<T> / ReadOnlySpan<T>
async / await
Task.WhenAll
CancellationToken
PeriodicTimer
IAsyncEnumerable<T>
Channel<T> (only where streaming/backpressure needs it)
System.Diagnostics.Process
System.Net / IPAddress / sockets
LibraryImport / P/Invoke basics
SafeHandle and ownership concepts
Linux-only platform boundaries
```

The goal is not to tick off language features. You should be able to point to a concrete ReproBox problem that made each one useful.

---

## Where to Stop

You do not need all 18 phases.

A strong portfolio release is already:

```text
✓ run commands
✓ inspect processes
✓ capture process tree
✓ inspect file descriptors
✓ monitor CPU/RAM/I/O
✓ trace filesystem activity
✓ summarize syscalls
✓ inspect network sockets
✓ isolate with namespaces
✓ apply cgroup limits
✓ export execution reports
```

That is roughly Phases 1–12.

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
11. Ask GPT about specific things you do not understand.
12. Refactor only after it works: ask whether a C# feature (`Span<T>`, async streams, `SafeHandle`, etc.) solves a real problem you encountered.
13. Write both the Linux concept **and the C# concept** you learned in `LEARNING.md`.
```

Do not ask:

```text
"Implement Phase 7 for me."
```

Prefer:

```text
"I expected openat to appear here but it doesn't. What assumption am I getting wrong?"
```

---

## Existing Tools to Learn First

Before implementing a feature, understand the Linux tool that exposes the same concept.

| ReproBox feature | Explore first          |

| ---------------- | ---------------------- |

| Processes        | `ps`, `/proc`          |

| Process tree     | `pstree`               |

| File descriptors | `lsof`, `/proc/*/fd`   |

| Signals          | `kill`                 |

| Syscalls         | `strace`               |

| Network          | `ss`                   |

| Memory           | `pmap`, `/proc/*/maps` |

| Namespaces       | `unshare`, `nsenter`   |

| Mounts           | `mount`, `findmnt`     |

| cgroups          | `/sys/fs/cgroup`       |

| Capabilities     | `capsh`, `getcap`      |

| eBPF             | `bpftrace`             |

The goal is not to rewrite these tools.

The goal is to understand the Linux primitives underneath them and combine them into something useful.

---

## Final Project Goal

A mature ReproBox should eventually let you do:

```bash
reprobox learn npm test
```

and answer:

```text
What processes did it create?
What files did it need?
What network did it use?
How much CPU/RAM did it consume?
Which syscalls did it use?
Can I reproduce this execution?
Can I run it with fewer permissions?
```

That is the project.
