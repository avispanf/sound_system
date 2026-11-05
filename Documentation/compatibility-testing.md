# Reproduce compatibility tests

Create an isolated package-only project from the repository root:

```sh
python3 Tools/prepare_test_project.py --destination /path/to/empty/AudioMWTest --unity-version 2022.3.62f3
```

The destination must be empty or carry the generator's ownership marker.
Re-running replaces only the copied AudioMW package and generated manifest /
version settings. Use a separate directory for each Unity version. The fixture
uses Test Framework 1.1.33 for 2022.3 or 1.6.0 for Unity 6 plus the required
built-in modules; it does not copy URP, MCP or other development packages.

Run the selected editor with `-batchmode -projectPath <fixture> -runTests
-testPlatform EditMode -testResults <absolute.xml> -logFile <absolute.log>`.
Repeat with PlayMode. Do not run two editors on the same fixture concurrently.

GitHub Actions now prepares this fixture before the existing version matrix.
It still requires working Unity licensing credentials. Local results do not
prove a cloud run or Linux image availability. Configuration follows the
[GameCI test runner](https://game.ci/docs/github/test-runner/).

This fixture checks the source package under Assets. It is not an import test
of the eventual UPM archive, Asset Store download or sample distribution.
