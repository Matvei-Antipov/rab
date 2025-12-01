"""
Example Python code for testing Code Preview feature
"""
import os
import sys
from typing import List, Optional


class CodePreviewDemo:
    """Demo class for code preview testing."""
    
    def __init__(self, name: str):
        self.name = name
        self.items: List[str] = []
    
    def add_item(self, item: str) -> None:
        """Add an item to the list."""
        if item:
            self.items.append(item)
            print(f"Added: {item}")
        else:
            raise ValueError("Item cannot be empty")
    
    def get_items(self) -> List[str]:
        """Return all items."""
        return self.items.copy()
    
    def process_data(self, data: dict) -> Optional[str]:
        """Process some data and return result."""
        if not data:
            return None
        
        result = []
        for key, value in data.items():
            if isinstance(value, (int, float)):
                result.append(f"{key}: {value * 2}")
            else:
                result.append(f"{key}: {value}")
        
        return "\n".join(result)


def main():
    """Main function to demonstrate the code."""
    demo = CodePreviewDemo("Test")
    
    # Add some items
    demo.add_item("Python")
    demo.add_item("JavaScript")
    demo.add_item("C#")
    
    # Process data
    data = {
        "count": 42,
        "name": "example",
        "ratio": 3.14
    }
    
    result = demo.process_data(data)
    print(result)
    
    # Get all items
    items = demo.get_items()
    print(f"Total items: {len(items)}")


if __name__ == "__main__":
    main()
