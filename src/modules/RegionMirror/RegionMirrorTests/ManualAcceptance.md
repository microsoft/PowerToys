# RegionMirror manual acceptance

Record the PowerToys commit, OS build, GPU, Teams version, display topology, scaling factors, and outcome for each check. Run after a successful final acceptance build. Mark unchecked items as not tested; unit and fuzz tests cover geometry and parsing only.

## Region selection and coordinates

- [ ] On a single ultrawide display, select a region containing two applications. Share the RegionMirror virtual display in Teams and switch between those applications without restarting sharing.
- [ ] Drag in all four directions. The visible border and shared content agree on the exact selected edges.
- [ ] Cancel before dragging, during dragging, and after opening the selector again. No selection overlay, capture session, or stale mirror remains after cancellation or exit.
- [ ] Click without dragging and select a line with zero width or height. No capture starts and the app remains usable.
- [ ] On a second display placed left of or above the primary display, use a region with negative screen coordinates. The output contains the selected pixels, with no origin offset.
- [ ] With two displays at different DPI settings (for example, 100% and 150%), select a region spanning both displays, then repeat after swapping their positions. The combined output preserves physical-pixel positions and crop edges, without applying logical-coordinate scaling to either tile.
- [ ] Drag across adjacent monitor boundaries and pass the equivalent cross-monitor CLI region. Both include every intersecting monitor in the correct position; repeat across horizontally arranged and vertically stacked displays, including negative origins.
- [ ] Select across displays with different sizes and staggered positions so the region includes a desktop gap. That gap remains black while the surrounding tiles update, without shifting, stretching, or repeating nearby pixels into it.
- [ ] Use a region exactly touching a neighboring monitor's edge without overlapping its area. That neighbor contributes no tile. Extend the selection one pixel across the edge and verify that exactly that strip is included.
- [ ] Pass malformed CLI values, dimensions of zero or 16385, and coordinates whose right/bottom endpoint would overflow a signed 32-bit integer. The app exits or reports the error without creating a capture session.

## Capture and sharing

- [ ] Update text, scroll, play animation, and move the pointer inside the region. The local mirror and a remote meeting participant's view update throughout the test.
- [ ] For a cross-monitor selection, animate each source monitor in turn while the other sources remain static, then animate them together. Each tile updates independently without freezing or blanking another monitor's latest frame; gaps remain black.
- [ ] Leave the source static, then update it after a pause. Capture resumes without a frozen image or stale frame.
- [ ] Resize the output to both wide and tall aspect ratios. Content stays within the client area, retains its aspect ratio, and is centered within the unused space.
- [ ] Move the output over the captured source region while it is shared. Confirm that recursive self-capture is prevented and that the source border is excluded as intended.
- [ ] Minimize and restore the output, then stop and restart Teams window sharing. Record whether frames resume and whether the meeting client retains the correct target.
- [ ] Check the selected-region boundary from the remote participant's view. Windows or notifications outside it must not become visible through a crop or DPI error.

## Lifetime and display changes

- [ ] Close the output while frames are arriving, repeat start/stop, and exit during selection. The process exits cleanly and capture borders/overlays disappear.
- [ ] Disconnect each contributing source monitor while mirroring a cross-monitor region. Capture stops or reports the loss cleanly, with no crash, stuck overlay, or stale frame presented as live content.
- [ ] Change any contributing source monitor's resolution, scaling, orientation, or position. An invalidated source region is stopped or revalidated before further frames are shown.
- [ ] Lock and unlock Windows, sleep and resume, and disconnect/reconnect Remote Desktop if available. Record recovery behavior and confirm no stale capture resources remain after exit.

This checklist describes acceptance criteria, not a claim that those scenarios have been validated.
