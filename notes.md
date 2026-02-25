## LLM note

Use this intro in chats:

```
This is an app that allows user to control the mouse using keyboard keys.
A grid is displayed spanning the entirety of the current screen when user activates it using a hotkey. Additional key listeners are added when this is activated. Labels are displayed within those cells.
When user presses these keys, the sections within the 3x3 grid are drilled into and become the new focus grid - so the mouse moves to the center of that grid section, and a smaller grid is displayed on screen with further sub divisions.
When user presses the uppercase K the grid is reset and mouse is moved to center of current screen.
The other directional keys move the mouse and grid manually.
A trail line is displayed on the screen for a brief moment when the mouse jumps from one location to another.
When y/n keys are pressed, left/right mouse click inputs are applied.
```

## TODOs

- Feat: make framerate independent move - don't hardcode it to ~60fps
- Click using keys
    - left and right
    - click and hold should be possible - don't send double click when holding key
- Make trail line animated and fade
- Be able to save spots on monitor and keybinds to jump to them
    - Feat: Record and replay clicks
- Draw diagram of current architecture 
- Refactor code
    - separate platform and app logic completely
    - effectively the app is just something that translates an input to another input
    - platform
        - input keyboard events
        - output mouse events
        - draw UI
    - app
        - translating input to output
        - UI - show grid, labels, legend, mouse movement (maybe)
        - handle configuration
        - state management
            - current position
            - current grid section
- Diagram of the updated architecture after refactor
- Fix: When opening and closing MSPaint after overlay is shown, all visual components disappear.

## Future nice to have features

- Legend
- Be able to track where the mouse moved to
    - Options
        - Animated cursor that goes to next position
        - Draw an animated line from previous to next position
    - Make this configurable with fps for those that want smooth animations
- Click and drag feature
- Scroll feature
- Configurable keybinds
