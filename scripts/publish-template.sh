#!/bin/bash
# Bash script to publish the Krafter template to NuGet
# Usage: ./scripts/publish-template.sh --api-key "your-api-key" [--source "nuget-source"]

set -e

API_KEY=""
SOURCE="https://api.nuget.org/v3/index.json"
CONFIGURATION="Release"
SKIP_BUILD=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --api-key)
            API_KEY="$2"
            shift 2
            ;;
        --source)
            SOURCE="$2"
            shift 2
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        *)
            echo "Unknown option: $1"
            echo "Usage: $0 --api-key <key> [--source <source>] [--skip-build]"
            exit 1
            ;;
    esac
done

if [ -z "$API_KEY" ]; then
    echo "❌ API key is required!"
    echo "Usage: $0 --api-key <key> [--source <source>] [--skip-build]"
    exit 1
fi

echo "🚀 Publishing Krafter Template to NuGet..."

# Navigate to root directory
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(dirname "$SCRIPT_DIR")"
cd "$ROOT_DIR"

# Build if not skipped
if [ "$SKIP_BUILD" = false ]; then
    echo "🔨 Building template package..."
    
    # Clean previous builds
    rm -rf bin obj
    
    # Pack the template
    dotnet pack AditiKraft.Krafter.Templates.csproj -c $CONFIGURATION
fi

# Find the package
PACKAGE_PATH=$(find bin/$CONFIGURATION -name "*.nupkg" | head -n 1)

if [ -z "$PACKAGE_PATH" ]; then
    echo "❌ Package not found! Run without --skip-build to build first."
    exit 1
fi

PACKAGE_NAME=$(basename "$PACKAGE_PATH")
echo "📦 Package found: $PACKAGE_NAME"

# Confirm before publishing
echo ""
echo "⚠️  You are about to publish to: $SOURCE"
echo "Package: $PACKAGE_NAME"
read -p "Continue? (y/n) " -n 1 -r
echo

if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "❌ Publishing cancelled."
    exit 0
fi

# Push to NuGet
echo ""
echo "📤 Pushing to NuGet..."
dotnet nuget push "$PACKAGE_PATH" --source "$SOURCE" --api-key "$API_KEY"

echo ""
echo "✅ Successfully published to NuGet!"
echo "🌐 Package will be available at: https://www.nuget.org/packages/Krafter.Templates"
echo "⏱️  Note: It may take 5-10 minutes for the package to be indexed and searchable."
echo ""
echo "📋 Users can install with:"
echo "   dotnet new install Krafter.Templates"
