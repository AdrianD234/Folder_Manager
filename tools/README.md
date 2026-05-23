# Tools

This directory is reserved for repository-local helper tools.

Rules:

- Tools must not mutate real user folders unless explicitly designed, reviewed, and confirmed.
- Tools used by tests must operate only on temporary directories.
- Any tool that changes files must document its inputs, outputs, and safety boundaries.
- Do not add large tools or generated binaries to the repository.
