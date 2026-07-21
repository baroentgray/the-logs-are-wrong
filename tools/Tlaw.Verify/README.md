# TLAW repository verification

Run the complete local verification suite from the repository root:

```powershell
dotnet run --configuration Release --project tools/Tlaw.Verify -- --expected-head $(git rev-parse HEAD)
```

The command writes ignored evidence to `artifacts/verification/` and prints only `PASS` or `FAIL`.

The verifier records repository identity, clean-tree state, restore/build/test command evidence, structured TRX counters, `git diff --check`, the versioned Gate 0 SHA-256 manifest, established architecture-test outcomes, and `Domain` package references. An optional `--expected-base <sha>` adds a merge-base equality check.

`gate0-baseline.json` is outside frozen Gate 0 content. Its `sourceSha` is the approved BAR-30 base `4056157d8df6742d60711fa4a34b92364b2cb2dc`; it contains SHA-256 hashes for each frozen path.

GitHub Actions uses the same cross-platform console implementation. The workflow runs on `ubuntu-latest` because the repository pins a cross-platform .NET SDK in `global.json` and neither Unity nor Windows-only tooling is required. For pull requests it checks out `github.event.pull_request.head.sha`, not GitHub's synthetic merge commit. JSON, Markdown, command logs, and TRX output are uploaded even when verification fails.
