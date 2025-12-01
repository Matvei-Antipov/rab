# Uchat Client - WPF MVVM Application

## Architecture

This WPF application follows the MVVM (Model-View-ViewModel) pattern with dependency injection and modern C# practices.

### Project Structure

```
Uchat.Client/
├── Infrastructure/          # Core infrastructure components
│   ├── DependencyInjection.cs
│   ├── IMessageBus.cs
│   ├── MessageBus.cs
│   └── Messages/           # Message types for MessageBus
├── Services/               # Application services
│   ├── INavigationService.cs
│   ├── NavigationService.cs
│   ├── IThemeManager.cs
│   └── ThemeManager.cs
├── ViewModels/             # View models
│   ├── ViewModelBase.cs
│   ├── MainWindowViewModel.cs
│   └── WelcomeViewModel.cs
├── Views/                  # WPF views
│   ├── MainWindow.xaml
│   └── WelcomeView.xaml
├── Themes/                 # Theme resource dictionaries
│   └── DarkTheme.xaml
└── App.xaml               # Application entry point
```

## Key Features

### 1. Dependency Injection

The application uses `Microsoft.Extensions.Hosting` to configure dependency injection:

- Services are registered in `Infrastructure/DependencyInjection.cs`
- Services are resolved through the `IServiceProvider`
- View models and views are registered as transient or singleton services

### 2. Navigation

The `NavigationService` provides navigation between view models:

```csharp
navigationService.NavigateTo<WelcomeViewModel>();
navigationService.NavigateBack();
```

### 3. Message Bus

The `MessageBus` enables loosely-coupled communication between components:

```csharp
// Subscribe
messageBus.Subscribe<ThemeChangedMessage>(msg => { /* handle */ });

// Publish
messageBus.Publish(new ThemeChangedMessage(AppTheme.Dark));
```

### 4. Theme Management

The `ThemeManager` allows switching between themes at runtime:

```csharp
themeManager.ApplyTheme(AppTheme.Dark);
```

### 5. Logging

Serilog is configured to log to rolling files:

- Log location: `%LocalAppData%/Uchat/Logs/`
- Rolling interval: Daily
- Retention: 7 days

### 6. MVVM Infrastructure

#### ViewModelBase

Base class for all view models providing:
- `INotifyPropertyChanged` implementation (via `ObservableObject`)
- `IsBusy` property for loading states
- `Title` property for view titles
- Navigation lifecycle methods (`OnNavigatedTo`, `OnNavigatedFrom`)

#### Relay Commands

Commands are implemented using `CommunityToolkit.Mvvm`:

```csharp
[RelayCommand]
private void DoSomething()
{
    // Command implementation
}
```

This generates a `DoSomethingCommand` property automatically.

## StyleCop Compliance

All code follows StyleCop rules:
- Using directives inside namespaces
- One public type per file
- camelCase fields (no underscore prefix)
- `this.` prefix required for local calls
- XML documentation for public members

## Running the Application

The application bootstraps through `App.xaml.cs`:

1. Serilog logging is configured
2. Host is built with services registered
3. Host is started
4. Dark theme is applied
5. Main window is shown

## Adding New Views

1. Create a view model in `ViewModels/` inheriting from `ViewModelBase`
2. Create a view in `Views/` (Window or UserControl)
3. Register both in `DependencyInjection.cs`
4. Navigate using `INavigationService.NavigateTo<TViewModel>()`
