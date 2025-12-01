# Code Snippet Preview Feature

## Overview
This feature provides an intelligent code preview system for the Uchat messenger. When a user sends a code file (e.g., `.py`, `.cs`, `.js`), the system automatically detects it and displays a "Preview" button (eye icon) next to the attachment. Clicking this button opens a VS Code Dark Theme styled window with syntax highlighting.

## Features

### 1. **Code File Detection**
- Automatically detects code files by extension
- Supported languages:
  - C# (.cs)
  - JavaScript/TypeScript (.js, .ts, .jsx, .tsx)
  - Python (.py)
  - Java (.java)
  - C/C++ (.cpp, .c, .h, .hpp)
  - Go (.go)
  - Rust (.rs)
  - PHP (.php)
  - Ruby (.rb)
  - Swift (.swift)
  - Kotlin (.kt)
  - Scala (.scala)
  - HTML (.html)
  - CSS (.css, .scss, .sass)
  - JSON (.json)
  - XML (.xml)
  - YAML (.yaml, .yml)
  - SQL (.sql)
  - Shell scripts (.sh, .bash)
  - PowerShell (.ps1)
  - Markdown (.md)
  - And more...

### 2. **Preview UI**
- **Eye Icon Button**: Appears next to Download and Open buttons for code files
- **Color**: Cornflower blue (#6495ED) to stand out
- **Tooltip**: "Preview Code"
- **Visibility**: Only shown for files detected as code

### 3. **Code Preview Window**
- **VS Code Dark Theme**: Professional dark color scheme
  - Background: #1E1E1E
  - Foreground: #D4D4D4
  - Line numbers: #858585
  - Header: #252526
- **Features**:
  - Syntax highlighting using AvalonEdit
  - Line numbers
  - Read-only mode
  - Scrolling support (vertical and horizontal)
  - File information (name, language, line count, size)
- **Actions**:
  - **Copy Code**: Copies entire code content to clipboard
  - **Download**: Saves code to a local file
  - **Close**: Closes the preview window

### 4. **Syntax Highlighting Colors (VS Code Dark)**
- **Comments**: #6A9955 (green)
- **Strings**: #CE9178 (orange/brownish)
- **Numbers**: #B5CEA8 (light green)
- **Keywords**: #C586C0 (purple/pink)
- **Methods**: #DCDCAA (yellow)
- **Types**: #4EC9B0 (cyan/turquoise)

## Technical Implementation

### New Files Created

#### 1. `/Helpers/CodeLanguageDetector.cs`
Static helper class that:
- Maps file extensions to programming languages
- Provides language names for display
- Maps extensions to AvalonEdit syntax definitions
- Checks if a file is a code file

#### 2. `/Views/CodePreviewControl.xaml` & `.xaml.cs`
UserControl for displaying code with:
- AvalonEdit TextEditor component
- Header with file information (name, language, stats)
- VS Code Dark Theme styling
- Custom syntax highlighting colors

#### 3. `/Views/CodePreviewWindow.xaml` & `.xaml.cs`
Modal window that:
- Hosts CodePreviewControl
- Provides Copy, Download, Close buttons
- Centers on parent window
- Resizable with minimum size constraints

### Modified Files

#### 1. `/Uchat.Client.csproj`
- Added AvalonEdit NuGet package (v6.3.0.90)

#### 2. `/ViewModels/AttachmentViewModel.cs`
- Added `IsCodeFile` property (similar to `IsImage`, `IsAudio`)
- Returns true if `AttachmentType == Code`

#### 3. `/ViewModels/ChatViewModel.cs`
- Added `PreviewCodeCommand` RelayCommand
- Downloads code file content
- Opens CodePreviewWindow with the code
- Proper error handling and logging

#### 4. `/Views/ChatView.xaml`
- Added 6th column to attachment Grid
- Added Preview button (eye icon) in Grid.Column="4"
- Button visibility bound to `IsCodeFile` property
- Moved Open button to Grid.Column="5"

## Usage

### For Users
1. Send or receive a code file in a chat
2. Notice the blue eye icon next to the file attachment
3. Click the eye icon to preview the code
4. View code with syntax highlighting
5. Optionally copy or download the code
6. Close the preview window when done

### For Developers
```csharp
// Check if a file is code
bool isCode = CodeLanguageDetector.IsCodeFile("script.py");

// Get language name
string language = CodeLanguageDetector.GetLanguageName("app.cs"); // Returns "C#"

// Get syntax highlighting name for AvalonEdit
string? syntax = CodeLanguageDetector.GetAvalonSyntaxName("main.cpp"); // Returns "C++"

// Preview code (in ChatViewModel)
await PreviewCodeAsync(attachment);
```

## Visual Design

### Attachment Button Layout
```
[Icon] [FileName]    [Download] [Preview] [Open]
                        ↓          ↓        ↓
                      Gray       Blue     Gray
```

### Preview Window Structure
```
┌──────────────────────────────────────────┐
│ [Icon] code.py          Python • 45 lines│  ← Header
├──────────────────────────────────────────┤
│ 1  import os                             │
│ 2  import sys                            │  ← Code Editor
│ 3                                        │    (with syntax
│ 4  def main():                           │     highlighting)
│ 5      print("Hello")                    │
│ ...                                      │
├──────────────────────────────────────────┤
│ [Copy Code] [Download]         [Close]  │  ← Actions
└──────────────────────────────────────────┘
```

## Dependencies

- **AvalonEdit** (6.3.0.90): Powerful text editor with syntax highlighting
- **Existing Uchat services**: IFileAttachmentService, IErrorHandlingService

## Benefits

1. **Improved Code Sharing**: Developers can quickly preview code without downloading
2. **Better UX**: VS Code-like interface familiar to developers
3. **Syntax Highlighting**: Makes code easier to read and understand
4. **Copy Functionality**: Quick code copying for collaboration
5. **Multi-Language Support**: Wide range of programming languages

## Future Enhancements

Possible improvements:
- Code search within preview
- Font size adjustment
- Theme switching (light/dark)
- Print code functionality
- Code export to PDF
- Comparison with previous versions
- Inline editing with save back to chat
