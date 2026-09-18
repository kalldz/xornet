#!/bin/bash
# Build script for Linux - creates self-contained executable

set -e

CONFIGURATION="${1:-Release}"
RUNTIME="${2:-linux-x64}"
OUTPUT_PATH="publish/linux"

echo "Building Xornet for Linux..."
echo "Configuration: $CONFIGURATION"
echo "Runtime: $RUNTIME"

# Clean output
rm -rf "$OUTPUT_PATH"

# Publish self-contained
dotnet publish src/Xornet/Xornet.csproj \
    -c "$CONFIGURATION" \
    -r "$RUNTIME" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:DebugType=None \
    -p:DebugSymbols=false \
    -o "$OUTPUT_PATH"

# Copy assets
if [ -d "assets" ]; then
    cp -r assets "$OUTPUT_PATH/"
fi

# Create tarball
TAR_NAME="Xornet-${RUNTIME}.tar.gz"
tar -czf "$TAR_NAME" -C "$OUTPUT_PATH" .

echo ""
echo "Build complete!"
echo "Output: $OUTPUT_PATH"
echo "Archive: $TAR_NAME"
