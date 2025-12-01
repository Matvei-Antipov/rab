#!/bin/bash
# ============================================================================
# Script: provision-database.sh
# Description: Bash wrapper for Provision-Database.ps1
# Usage: ./provision-database.sh [--skip-oracle] [--skip-mongo] [--skip-redis]
# ============================================================================

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PS_SCRIPT="$SCRIPT_DIR/Provision-Database.ps1"

# Parse arguments
SKIP_ORACLE=""
SKIP_MONGO=""
SKIP_REDIS=""
VERBOSE=""

for arg in "$@"; do
    case $arg in
        --skip-oracle)
            SKIP_ORACLE="-SkipOracle"
            shift
            ;;
        --skip-mongo)
            SKIP_MONGO="-SkipMongo"
            shift
            ;;
        --skip-redis)
            SKIP_REDIS="-SkipRedis"
            shift
            ;;
        --verbose)
            VERBOSE="-Verbose"
            shift
            ;;
        *)
            echo "Unknown option: $arg"
            echo "Usage: $0 [--skip-oracle] [--skip-mongo] [--skip-redis] [--verbose]"
            exit 1
            ;;
    esac
done

# Check if PowerShell is installed
if ! command -v pwsh &> /dev/null; then
    echo "Error: PowerShell (pwsh) is not installed."
    echo "Please install PowerShell Core: https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell"
    exit 1
fi

# Run PowerShell script
echo "Running database provisioning script..."
pwsh -File "$PS_SCRIPT" $SKIP_ORACLE $SKIP_MONGO $SKIP_REDIS $VERBOSE
