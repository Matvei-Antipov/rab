# Testing Code Preview Feature

## Prerequisites
- Windows OS (for WPF)
- .NET 8.0 SDK
- Visual Studio 2022 or Visual Studio Code with C# extension

## Building the Project

```powershell
# Navigate to client project
cd uchat-mvp/src/Uchat.Client

# Restore NuGet packages
dotnet restore

# Build the project
dotnet build

# Run the application
dotnet run
```

## Testing Steps

### 1. Start the Application
1. Run the Uchat server first (if not already running)
2. Start the Uchat client application
3. Login or register an account

### 2. Send a Code File

#### Option A: Using Test Files
The repository includes test code files:
- `test_code_example.py` - Python example
- `test_code_example.cs` - C# example  
- `test_code_example.js` - JavaScript example

#### Option B: Create Your Own
Create any code file with supported extension:
- `.py`, `.cs`, `.js`, `.ts`, `.cpp`, `.java`, `.go`, `.rs`, etc.

#### Steps:
1. Open a chat conversation
2. Click the attachment button (paperclip icon)
3. Select a code file (e.g., `test_code_example.py`)
4. Send the message

### 3. Preview the Code

1. **Locate the Preview Button**
   - After sending, you'll see the file attachment in the chat
   - Look for the blue eye icon (👁️) next to Download and Open buttons
   - The eye icon should only appear for code files

2. **Click the Preview Button**
   - The Code Preview Window will open
   - Should display in VS Code Dark Theme

3. **Verify Features**
   - ✅ File name shown in header
   - ✅ Programming language detected (e.g., "Python")
   - ✅ Line count and file size displayed
   - ✅ Syntax highlighting applied
   - ✅ Line numbers visible on the left
   - ✅ Code is readable and properly formatted

4. **Test Actions**
   - **Copy Code**: Click "Copy Code" button
     - Paste somewhere to verify clipboard content
     - Should show success message
   
   - **Download**: Click "Download" button
     - Save dialog should appear
     - Save to a location
     - Verify file content matches
   
   - **Close**: Click "Close" button
     - Window should close
     - Should return to chat view

### 4. Test Different File Types

Test with various code file types to verify language detection:

#### Python (.py)
- Keywords: `import`, `def`, `class`, `if`, `else`, `return`
- Should show as "Python" in header

#### C# (.cs)
- Keywords: `using`, `namespace`, `class`, `public`, `private`
- Should show as "C#" in header

#### JavaScript (.js)
- Keywords: `const`, `let`, `function`, `class`, `return`
- Should show as "JavaScript" in header

#### HTML (.html)
- Tags should be highlighted
- Should show as "HTML" in header

#### JSON (.json)
- Keys and values should be highlighted
- Should show as "JSON" in header

### 5. Test Non-Code Files

Send a non-code file (e.g., `.txt`, `.pdf`, `.jpg`):
- Preview button (eye icon) should **NOT** appear
- Only Download and Open buttons should be visible

### 6. Verify Button Layout

For code files, the attachment should show buttons in this order:
```
[File Icon] [Filename.ext]     [Download] [Preview] [Open]
                                  (gray)    (blue)   (gray)
```

For non-code files:
```
[File Icon] [Filename.ext]     [Download] [Open]
                                  (gray)    (gray)
```

## Expected Behavior

### Code Preview Window
- **Size**: 1000x700 pixels (resizable)
- **Minimum Size**: 600x400 pixels
- **Position**: Centered on parent window
- **Background**: Dark (#1E1E1E)
- **Editor**: AvalonEdit with line numbers
- **Font**: Consolas or Courier New, size 14
- **Theme**: VS Code Dark

### Syntax Highlighting Colors
- **Comments**: Green (#6A9955)
- **Strings**: Orange/Brown (#CE9178)
- **Numbers**: Light Green (#B5CEA8)
- **Keywords**: Purple (#C586C0)
- **Methods**: Yellow (#DCDCAA)
- **Types**: Cyan (#4EC9B0)

### Error Handling
- If file download fails: Error message displayed
- If file is too large: Should still attempt to display
- If syntax highlighting not available: Plain text with line numbers

## Common Issues

### Issue 1: Preview Button Not Appearing
- **Cause**: File extension not recognized as code
- **Solution**: Check `FileHelper.cs` ExtensionToTypeMap for supported extensions

### Issue 2: No Syntax Highlighting
- **Cause**: Language not supported by AvalonEdit
- **Solution**: Code will display in plain text with line numbers

### Issue 3: Build Errors on Linux
- **Cause**: WPF requires Windows
- **Solution**: Build and test on Windows only

### Issue 4: AvalonEdit Not Found
- **Cause**: NuGet package not restored
- **Solution**: Run `dotnet restore` in Uchat.Client directory

## Debugging

### Enable Verbose Logging
Check Serilog logs for code preview operations:
```
[Information] Previewed code file: example.py
[Error] Failed to preview code: example.py - Reason: ...
```

### Verify File Download
The code is downloaded using `DownloadImageStreamAsync` method, which works for any file type despite its name.

### Check Attachment Type
In `AttachmentViewModel`:
```csharp
bool isCode = attachment.IsCodeFile; // Should be true for code files
AttachmentType type = attachment.AttachmentType; // Should be AttachmentType.Code
```

## Test Checklist

- [ ] Code file sent successfully
- [ ] Preview button (eye icon) appears for code files
- [ ] Preview button is blue (#6495ED)
- [ ] Preview button only for code files (not images/documents)
- [ ] Code preview window opens on click
- [ ] VS Code Dark Theme applied
- [ ] Syntax highlighting works
- [ ] Line numbers visible
- [ ] File info correct (name, language, lines, size)
- [ ] Copy Code button works
- [ ] Download button works
- [ ] Close button works
- [ ] Window is resizable
- [ ] Multiple previews work (open, close, open again)
- [ ] Different code file types work (Python, C#, JS, etc.)
- [ ] Non-code files don't show preview button
- [ ] Error handling works (network errors, etc.)

## Success Criteria

The feature is working correctly if:
1. ✅ Code files show preview button (eye icon)
2. ✅ Non-code files don't show preview button
3. ✅ Preview window displays code with VS Code Dark Theme
4. ✅ Syntax highlighting is applied correctly
5. ✅ All action buttons (Copy, Download, Close) work
6. ✅ Multiple file types are supported
7. ✅ No crashes or errors during normal use
