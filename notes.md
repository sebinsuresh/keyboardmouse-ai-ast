## TODOs

- Click using keys
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
