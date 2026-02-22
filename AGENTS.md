# AGENTS.md

## Code Style
- No excessive comments. Only comment tricky logic or unfamiliar Windows API behavior.
- Keep methods short and focused on a single concern.
- No tangled multi-concern logic in one method or class — separate via methods or classes.
- Clean, readable code over verbose code.

## unsafe Code
- Keep `unsafe` blocks as small as possible — wrap only the pointer dereference, not the surrounding logic.
- Example pattern used in this codebase:
  ```csharp
  unsafe
  {
      var kbd = (KBDLLHOOKSTRUCT*)lParam.Value;
      // minimal pointer work here
  }
  ```

## Win32 Interop (CsWin32)
- All Win32 interop uses `Microsoft.Windows.CsWin32` source generation.
- **Never use `[DllImport]` or `[LibraryImport]` manually.**
- To use a new Win32 API: add its name to `NativeMethods.txt`, then build. Generated `PInvoke.*` methods and types are available after build (IDE red squiggles before build are expected).
- Keep `NativeMethods.txt` small — only add APIs that are actually used.
- CsWin32 is on its latest version; do not use outdated syntax patterns.
