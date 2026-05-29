# Thought Flow

Thought Flow is a small WPF writing app built around a simple idea: workspaces contain text files, and each file is written as a stream of message-sized chunks.

## Run

Run RunThoughtFlow.bat

## Current Features

- Workspaces in the left sidebar.
- Text files inside each workspace.
- Choose a folder location when creating a workspace.
- Workspace files are synced to that folder as `.md` files.
- Continuous message stream in the center.
- Hover a message in the stream to see which chunk will open in the editor.
- Use the hover `...` menu to edit or delete a specific message.
- Right-click workspaces, files, and messages for contextual actions.
- Select stream text across multiple messages and copy it like normal text.
- Full editor for the selected message on the right.
- Create and delete workspaces.
- Create and delete files.
- Confirm workspace deletion before it happens.
- Confirm workspace and file deletion before it happens.
- Delete confirmations use the app's own styled dialog.
- Undo deleted workspaces, files, and messages for 7 seconds; multiple undo prompts stack as a small list.
- Send messages with the button or `Ctrl+Enter`.
- Search messages inside the active file.
- Duplicate, copy, delete, and save messages.
- Mark selected editor text with a background color chosen from the palette.
- Render `""bold text""` as bold in the message stream while keeping the raw markup editable.
- Render `<<italic text>>`, `__underlined text__`, `~~struck text~~`, and `` `inline code` `` in the message stream.
- Render `||spoiler text||` as a click-to-reveal spoiler in the message stream while keeping the raw markup editable.
- Automatic JSON storage in `%LOCALAPPDATA%\ThoughtFlow\library.json`.

## Upcoming Updates

- Markdown export.
- Tags and pinned notes.
- Focus mode.
- Rename spaces.
- Drag notes between spaces.
- Better typography settings.
