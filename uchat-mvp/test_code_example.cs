using System;
using System.Collections.Generic;
using System.Linq;

namespace Uchat.Examples
{
    /// <summary>
    /// Example C# code for testing Code Preview feature.
    /// </summary>
    public class CodePreviewDemo
    {
        private readonly string name;
        private readonly List<string> items;

        /// <summary>
        /// Initializes a new instance of the <see cref="CodePreviewDemo"/> class.
        /// </summary>
        /// <param name="name">The name of the demo.</param>
        public CodePreviewDemo(string name)
        {
            this.name = name;
            this.items = new List<string>();
        }

        /// <summary>
        /// Gets the name of the demo.
        /// </summary>
        public string Name => this.name;

        /// <summary>
        /// Gets the count of items.
        /// </summary>
        public int ItemCount => this.items.Count;

        /// <summary>
        /// Adds an item to the collection.
        /// </summary>
        /// <param name="item">The item to add.</param>
        public void AddItem(string item)
        {
            if (string.IsNullOrWhiteSpace(item))
            {
                throw new ArgumentException("Item cannot be empty", nameof(item));
            }

            this.items.Add(item);
            Console.WriteLine($"Added: {item}");
        }

        /// <summary>
        /// Gets all items in the collection.
        /// </summary>
        /// <returns>A copy of the items list.</returns>
        public List<string> GetItems()
        {
            return new List<string>(this.items);
        }

        /// <summary>
        /// Processes data and returns a formatted result.
        /// </summary>
        /// <param name="data">The data to process.</param>
        /// <returns>The formatted result string.</returns>
        public string ProcessData(Dictionary<string, object> data)
        {
            if (data == null || !data.Any())
            {
                return string.Empty;
            }

            var results = new List<string>();

            foreach (var kvp in data)
            {
                if (kvp.Value is int || kvp.Value is double)
                {
                    var numValue = Convert.ToDouble(kvp.Value);
                    results.Add($"{kvp.Key}: {numValue * 2}");
                }
                else
                {
                    results.Add($"{kvp.Key}: {kvp.Value}");
                }
            }

            return string.Join(Environment.NewLine, results);
        }

        /// <summary>
        /// Main entry point for demonstration.
        /// </summary>
        public static void Main()
        {
            var demo = new CodePreviewDemo("Test");

            // Add some items
            demo.AddItem("C#");
            demo.AddItem("Python");
            demo.AddItem("JavaScript");

            // Process data
            var data = new Dictionary<string, object>
            {
                { "count", 42 },
                { "name", "example" },
                { "ratio", 3.14 }
            };

            var result = demo.ProcessData(data);
            Console.WriteLine(result);

            // Display item count
            Console.WriteLine($"Total items: {demo.ItemCount}");
        }
    }
}
