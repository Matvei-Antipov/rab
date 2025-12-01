# Search-as-you-type, Settings Navigation & Back Button Implementation

## Overview
This document describes the implementation of three new features:
1. **Search-as-you-type** - Automatic message search without pressing Enter or search button
2. **Settings Navigation Button** - Button on lower left panel to open Settings view
3. **Back Button in Settings** - Return to Chat view from Settings

## 1. Search-as-you-type Feature

### Implementation Details

#### Files Modified
- `src/Uchat.Client/ViewModels/ChatViewModel.cs`

#### Changes Made

1. **Added search cancellation token field**:
```csharp
private CancellationTokenSource? searchCancellationTokenSource;
```

2. **Modified MessageSearchText property**:
```csharp
public string MessageSearchText
{
    get => this.messageSearchText;
    set
    {
        if (this.SetProperty(ref this.messageSearchText, value))
        {
            this.ClearMessageSearchCommand.NotifyCanExecuteChanged();
            this.SearchMessagesInChatCommand.NotifyCanExecuteChanged();
            _ = this.TriggerSearchAsYouTypeAsync(); // NEW LINE
        }
    }
}
```

3. **Added debounced search method**:
```csharp
private async Task TriggerSearchAsYouTypeAsync()
{
    this.searchCancellationTokenSource?.Cancel();
    this.searchCancellationTokenSource = new CancellationTokenSource();
    var token = this.searchCancellationTokenSource.Token;

    try
    {
        await Task.Delay(300, token); // 300ms debounce

        if (token.IsCancellationRequested)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(this.MessageSearchText))
        {
            await this.LoadMessagesForSelectedConversationAsync();
        }
        else
        {
            await this.SearchMessagesInChatAsync();
        }
    }
    catch (TaskCanceledException)
    {
        // Ignored - normal when user types quickly
    }
}
```

### How It Works

1. **User types a character** → `MessageSearchText` property changes
2. **Debounce starts** → Previous search request cancelled, 300ms timer starts
3. **User continues typing** → Timer resets, previous search cancelled
4. **User stops typing for 300ms** → Search executes automatically
5. **Empty search box** → All messages automatically reloaded

### Benefits

- ✅ No need to press Enter or click search button
- ✅ Prevents excessive server calls during fast typing
- ✅ Smooth user experience with 300ms delay
- ✅ Automatically clears results when search box is empty
- ✅ Existing Enter/button functionality still works

## 2. Settings Navigation Button

### Implementation Details

#### Files Modified
- `src/Uchat.Client/ViewModels/MainWindowViewModel.cs`
- `src/Uchat.Client/Views/MainWindow.xaml`
- `src/Uchat.Client/Views/ChatView.xaml`
- `src/Uchat.Client/Views/SettingsView.xaml`

#### Changes Made

1. **MainWindowViewModel - Added navigation commands**:

```csharp
private readonly INavigationService navigationService;

[ObservableProperty]
private object? currentView;

[RelayCommand]
private void NavigateToChat()
{
    this.CurrentView = null;
    this.navigationService.NavigateTo<ChatViewModel>();
}

[RelayCommand]
private void NavigateToGenerator()
{
    this.CurrentView = null;
    this.navigationService.NavigateTo<GeneratorViewModel>();
}

[RelayCommand]
private void NavigateToSettings()
{
    if (this.CurrentViewModel != null)
    {
        this.CurrentViewModel.OnNavigatedFrom();
    }

    var settingsView = new SettingsView();
    this.SetCurrentView(settingsView);
}
```

2. **MainWindow.xaml - Added CurrentView ContentControl**:

```xaml
<Grid Grid.Row="1">
    <ContentControl Content="{Binding CurrentViewModel}"
                    HorizontalAlignment="Stretch"
                    VerticalAlignment="Stretch"
                    Focusable="False"
                    Margin="0"/>
    <ContentControl Content="{Binding CurrentView}"
                    HorizontalAlignment="Stretch"
                    VerticalAlignment="Stretch"
                    Focusable="False"
                    Margin="0"/>
</Grid>
```

3. **ChatView.xaml - Settings button with command**:

```xaml
<Button Command="{Binding DataContext.NavigateToSettingsCommand, 
                          RelativeSource={RelativeSource AncestorType=Window}}" 
        Style="{StaticResource RoundNavButtonStyle}" 
        Background="#4E5165" 
        ToolTip="Settings">
    <TextBlock Text="&#xE713;" FontFamily="Segoe MDL2 Assets" FontSize="18"/>
</Button>
```

### Navigation Architecture

SettingsView doesn't have a ViewModel (it's a static settings page). Navigation is handled via:

- **CurrentViewModel** - For views with ViewModels (ChatView, GeneratorView, etc.)
- **CurrentView** - For views without ViewModels (SettingsView)
- MainWindow displays both, with priority given to CurrentViewModel

## 3. Back Button in Settings

### Implementation Details

#### Files Modified
- `src/Uchat.Client/Views/SettingsView.xaml`

#### Changes Made

**Added Back button in top-left corner of Settings panel**:

```xaml
<Grid Grid.Row="0" Margin="35,25">
    <Button Command="{Binding DataContext.NavigateToChatCommand, 
                              RelativeSource={RelativeSource AncestorType=Window}}" 
            Style="{StaticResource RoundNavButtonStyle}" 
            Width="36" Height="36" 
            HorizontalAlignment="Left" 
            Background="#33354E" 
            ToolTip="Back to Chat">
        <TextBlock Text="&#xE72B;" FontFamily="Segoe MDL2 Assets" FontSize="16"/>
    </Button>
    <TextBlock Text="Settings" FontSize="28" Foreground="#E0E0E0" 
               FontWeight="Light" VerticalAlignment="Center" 
               HorizontalAlignment="Center"/>
    <!-- Right side content remains unchanged -->
</Grid>
```

### Button Details

- **Icon**: `&#xE72B;` (Back arrow from Segoe MDL2 Assets)
- **Size**: 36x36 pixels (slightly smaller than nav panel buttons)
- **Position**: Top-left corner, left-aligned within margin
- **Style**: Uses same `RoundNavButtonStyle` as navigation buttons
- **Background**: `#33354E` (same as home button)
- **Command**: `NavigateToChatCommand` from MainWindowViewModel

## Left Panel Navigation Consistency

All three views (ChatView, SettingsView, GeneratorView) now have consistent navigation:

### Button Order (bottom panel, top to bottom):
1. **Gallery** - `&#xE91B;` → NavigateToGeneratorCommand → Background: `#5B4B6F` (or `#6E7195` when active)
2. **Theme** - `&#xE708;` → ToggleThemeCommand → Background: `#8C7B30`
3. **Settings** - `&#xE713;` → NavigateToSettingsCommand → Background: `#4E5165` (or `#6E7195` when active)

All buttons use `RelativeSource={RelativeSource AncestorType=Window}` binding to access MainWindowViewModel commands.

## Testing Checklist

### Search-as-you-type
- [ ] Type single letter → Search triggers after 300ms
- [ ] Type quickly → Only last search executes (previous cancelled)
- [ ] Clear search box → All messages reload automatically
- [ ] Press Enter → Search still works
- [ ] Click search button → Search still works

### Settings Navigation
- [ ] Click Settings button in ChatView → Opens Settings
- [ ] Click Settings button in GeneratorView → Opens Settings
- [ ] Click Settings button in SettingsView → Stays in Settings (active state)
- [ ] Click Back button in Settings → Returns to Chat

### Back Button
- [ ] Back button visible in top-left of Settings
- [ ] Back button returns to ChatView
- [ ] Back button has correct icon (left arrow)
- [ ] Back button has hover effect

## Migration Notes

### Breaking Changes
None - all changes are additive and backward compatible.

### Dependencies
- Existing MVVM Toolkit RelayCommand infrastructure
- Existing NavigationService implementation
- No new NuGet packages required

## Future Improvements

1. **Search History**: Store recent searches for quick access
2. **Search Highlighting**: Highlight matched text in search results
3. **Search Filters**: Filter by sender, date range, attachment type
4. **Settings Persistence**: Save user settings to local storage
5. **Navigation History**: Remember last view before opening Settings
