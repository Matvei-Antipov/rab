namespace Uchat.Client.Views
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Media;
    using ICSharpCode.AvalonEdit.Highlighting;
    using ICSharpCode.AvalonEdit.Highlighting.Xshd;
    using ICSharpCode.AvalonEdit.Rendering;
    using Uchat.Client.Helpers;
    using Uchat.Shared.Helpers;

    /// <summary>
    /// Interaction logic for CodePreviewControl.xaml.
    /// </summary>
    public partial class CodePreviewControl : UserControl
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodePreviewControl"/> class.
        /// </summary>
        public CodePreviewControl()
        {
            this.InitializeComponent();
            this.ApplyVSCodeDarkTheme();
        }

        /// <summary>
        /// Loads code from text content.
        /// </summary>
        /// <param name="code">The code content.</param>
        /// <param name="fileName">The file name.</param>
        public void LoadCode(string code, string fileName)
        {
            this.CodeEditor.Text = code;
            this.FileNameTextBlock.Text = fileName;

            var language = CodeLanguageDetector.GetLanguageName(fileName);
            this.LanguageTextBlock.Text = language;

            var lines = code.Split('\n').Length;
            var size = System.Text.Encoding.UTF8.GetByteCount(code);
            this.LineSizeTextBlock.Text = $"{lines} lines • {FileHelper.FormatFileSize(size)}";

            this.ApplySyntaxHighlighting(fileName);
        }

        private void ApplyVSCodeDarkTheme()
        {
            this.CodeEditor.Background = new SolidColorBrush(Color.FromRgb(30, 30, 30));
            this.CodeEditor.Foreground = new SolidColorBrush(Color.FromRgb(212, 212, 212));
            this.CodeEditor.LineNumbersForeground = new SolidColorBrush(Color.FromRgb(133, 133, 133));

            this.CodeEditor.TextArea.TextView.CurrentLineBackground = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            this.CodeEditor.TextArea.TextView.CurrentLineBorder = new Pen(new SolidColorBrush(Color.FromArgb(0, 255, 255, 255)), 0);

            this.CodeEditor.TextArea.SelectionBorder = new Pen(new SolidColorBrush(Color.FromRgb(38, 79, 120)), 1);
            this.CodeEditor.TextArea.SelectionBrush = new SolidColorBrush(Color.FromArgb(100, 38, 79, 120));
            this.CodeEditor.TextArea.SelectionForeground = null;
        }

        private void ApplySyntaxHighlighting(string fileName)
        {
            var syntaxName = CodeLanguageDetector.GetAvalonSyntaxName(fileName);
            if (syntaxName != null)
            {
                try
                {
                    var highlightingDefinition = HighlightingManager.Instance.GetDefinition(syntaxName);
                    if (highlightingDefinition != null)
                    {
                        this.CodeEditor.SyntaxHighlighting = highlightingDefinition;

                        this.CustomizeHighlightingColors();
                    }
                }
                catch
                {
                }
            }
        }

        private void CustomizeHighlightingColors()
        {
            if (this.CodeEditor.SyntaxHighlighting == null)
            {
                return;
            }

            var clonedHighlighting = new CustomHighlightingDefinition(this.CodeEditor.SyntaxHighlighting);
            this.CodeEditor.SyntaxHighlighting = clonedHighlighting;
        }

        private class CustomHighlightingDefinition : IHighlightingDefinition
        {
            private readonly IHighlightingDefinition baseDefinition;

            public CustomHighlightingDefinition(IHighlightingDefinition baseDefinition)
            {
                this.baseDefinition = baseDefinition;
            }

            public string Name => this.baseDefinition.Name;

            public HighlightingRuleSet? MainRuleSet => this.baseDefinition.MainRuleSet;

            public HighlightingColor? GetNamedColor(string name)
            {
                var color = this.baseDefinition.GetNamedColor(name);
                if (color == null)
                {
                    return null;
                }

                var customColor = new HighlightingColor
                {
                    Name = color.Name,
                    FontWeight = color.FontWeight,
                    FontStyle = color.FontStyle,
                };

                switch (name)
                {
                    case "Comment":
                        customColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(106, 153, 85));
                        break;
                    case "String":
                    case "Char":
                        customColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(244, 112, 103));
                        break;
                    case "NumberLiteral":
                    case "Number":
                        customColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(107, 182, 255));
                        break;
                    case "Keywords":
                    case "Keyword":
                        customColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(244, 112, 103));
                        break;
                    case "MethodCall":
                    case "MethodName":
                        customColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(246, 157, 81));
                        break;
                    case "TypeKeywords":
                    case "ReferenceTypes":
                    case "ValueTypes":
                        customColor.Foreground = new SimpleHighlightingBrush(Color.FromRgb(246, 157, 81));
                        break;
                    default:
                        customColor.Foreground = color.Foreground;
                        customColor.Background = color.Background;
                        break;
                }

                return customColor;
            }

            public HighlightingRuleSet? GetNamedRuleSet(string name)
            {
                return this.baseDefinition.GetNamedRuleSet(name);
            }

            public System.Collections.Generic.IEnumerable<HighlightingColor> NamedHighlightingColors => this.baseDefinition.NamedHighlightingColors;

            public System.Collections.Generic.IDictionary<string, string> Properties => this.baseDefinition.Properties;
        }

        private class SimpleHighlightingBrush : HighlightingBrush
        {
            private readonly SolidColorBrush brush;

            public SimpleHighlightingBrush(Color color)
            {
                this.brush = new SolidColorBrush(color);
                this.brush.Freeze();
            }

            public override Brush GetBrush(ITextRunConstructionContext context)
            {
                return this.brush;
            }

            public override string ToString()
            {
                return this.brush.ToString();
            }
        }
    }
}
