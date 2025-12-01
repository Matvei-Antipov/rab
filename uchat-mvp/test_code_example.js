/**
 * Example JavaScript code for testing Code Preview feature
 */

class CodePreviewDemo {
    /**
     * Creates a new CodePreviewDemo instance
     * @param {string} name - The name of the demo
     */
    constructor(name) {
        this.name = name;
        this.items = [];
    }

    /**
     * Adds an item to the collection
     * @param {string} item - The item to add
     * @throws {Error} If item is empty
     */
    addItem(item) {
        if (!item) {
            throw new Error('Item cannot be empty');
        }
        
        this.items.push(item);
        console.log(`Added: ${item}`);
    }

    /**
     * Gets all items in the collection
     * @returns {Array<string>} A copy of the items array
     */
    getItems() {
        return [...this.items];
    }

    /**
     * Processes data and returns formatted result
     * @param {Object} data - The data to process
     * @returns {string} The formatted result
     */
    processData(data) {
        if (!data || Object.keys(data).length === 0) {
            return '';
        }

        const results = [];

        for (const [key, value] of Object.entries(data)) {
            if (typeof value === 'number') {
                results.push(`${key}: ${value * 2}`);
            } else {
                results.push(`${key}: ${value}`);
            }
        }

        return results.join('\n');
    }

    /**
     * Gets the count of items
     * @returns {number} The number of items
     */
    get itemCount() {
        return this.items.length;
    }
}

/**
 * Main function to demonstrate the code
 */
function main() {
    const demo = new CodePreviewDemo('Test');

    // Add some items
    demo.addItem('JavaScript');
    demo.addItem('Python');
    demo.addItem('C#');

    // Process data
    const data = {
        count: 42,
        name: 'example',
        ratio: 3.14
    };

    const result = demo.processData(data);
    console.log(result);

    // Display item count
    console.log(`Total items: ${demo.itemCount}`);
}

// Run the main function
if (require.main === module) {
    main();
}

module.exports = CodePreviewDemo;
