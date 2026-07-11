# win-x64 Tree-sitter native DLLs (pending)

This folder is where `tree-sitter-limelightx.dll` and `tree-sitter-runtime.dll`
land once a win-x64 build exists. It is currently empty — see
`spec/parsing/tree-sitter-build-guide.md` §9 for the build steps (an
**x64 Native Tools Command Prompt for VS 2022** instead of the ARM64 one) and
`spec/parsing/tree-sitter-runtime-build-guide.md` §3 for the runtime DLL's
equivalent.

`LimelightX.UI.csproj` already resolves this folder automatically for any
`win-x64` build/publish/test — dropping both DLLs in here is the only step
required; no csproj, CI, or script change is needed on top of it.
