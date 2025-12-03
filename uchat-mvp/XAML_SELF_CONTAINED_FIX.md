# XAML Self-Contained Resources Fix

## Проблема
При запуске возникала ошибка `System.Windows.StaticResourceExtension` из-за зависимости от внешних словарей ресурсов (DarkTheme.xaml, LightTheme.xaml).

## Решение
Сделаны оба файла предпросмотра кода полностью автономными (self-contained):

### 1. CodePreviewWindow.xaml
- Добавлена секция `Window.Resources` с определениями всех необходимых кистей (Brushes):
  - `CodeBackgroundBrush` (#1E1E1E)
  - `CodeHeaderBackgroundBrush` (#252526)
  - `CodeBorderBrush` (#3E3E42)
  - `CodeTextBrush` (#CCCCCC)
  - `CodeTextSecondaryBrush` (#858585)
  - `CodeButtonBackgroundBrush` (#2D2D30)
  - `CodeButtonHoverBrush` (#3E3E42)
  - `CodeButtonPressedBrush` (#007ACC)
  - `CodeCloseButtonBrush` (#3A3A3A)
- Стиль `ModernButtonStyle` уже был определен внутри файла и использует hardcoded значения

### 2. CodePreviewControl.xaml
- Добавлена секция `UserControl.Resources` с определениями всех необходимых кистей:
  - `CodeBackgroundBrush` (#1E1E1E)
  - `CodeHeaderBackgroundBrush` (#252526)
  - `CodeBorderBrush` (#3E3E42)
  - `CodeTextBrush` (#CCCCCC)
  - `CodeTextSecondaryBrush` (#858585)
- Все цвета в разметке уже были прописаны как hardcoded hex values

## Результат
Оба файла теперь полностью автономны и не требуют подключения внешних словарей ресурсов. 
Окно предпросмотра кода можно открыть в любом контексте без ошибок StaticResourceExtension.

## Совместимость
- Сохранена тема VS Code Dark
- Все цвета соответствуют оригинальному дизайну
- Код остается StyleCop-compliant
