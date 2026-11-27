---
name: trias-release
description: Validate TRIAS native builds and assemble evidence for release after environment, rendering, or gameplay changes.
---

# TRIAS release

Use the native scene and the existing `Trias.Preview.BuildTrias` entry points in [the Unity README](../../../README.md). Build a test artifact in a scratch checkout using the matching `ProjectVersion.txt`. Use production scenes and the same imported FBX settings. Capture the startup and gameplay smoke from a standalone player. The editor-only environment studio stays outside player build settings.

Check the versions of the native catalog and server when a socket match fails to load. Record platform, build type, maps, modes, output logs and known test gaps. Publishing requires separate authorization. Keep raw binaries and capture files in task outputs until a release is approved.
