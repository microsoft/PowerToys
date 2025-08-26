# Research Mode Module

This module is a work-in-progress implementation of the **Research Mode** utility proposed in issue #41379.

When enabled, Research Mode will monitor clipboard events and append copies of text or images to a Markdown log file in a user-specified folder. Each entry records metadata such as the timestamp, application process name, window title, and, if available, the source URL.

This directory contains the initial scaffolding for the module. Future contributions will implement:

- A clipboard listener to detect copy events.
- Conversion pipelines to convert clipboard formats (text, HTML, RTF) into Markdown using existing Advanced Paste functionality.
- File I/O logic to append entries to daily or rolling log files.
- Configuration UI integrated into PowerToys Settings to control destination folder, entry template, rollovers, and safety filters.
- Safety features like app allow/block lists, pattern shields, deduplication, and size thresholds.

This stub provides a starting point for development of Research Mode.
