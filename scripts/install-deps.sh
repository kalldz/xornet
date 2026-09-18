#!/bin/bash
# Install dependencies for Linux (Ubuntu/Debian)

set -e

echo "Installing Xornet dependencies for Linux..."

# Check if running as root
if [ "$EUID" -ne 0 ]; then
    echo "Please run with sudo"
    exit 1
fi

# Update package list
apt-get update

# Install libpcap development files
apt-get install -y libpcap-dev

# Install .NET 8.0 if not present
if ! command -v dotnet &> /dev/null; then
    echo "Installing .NET 8.0 SDK..."
    apt-get install -y wget
    wget https://packages.microsoft.com/config/ubuntu/$(lsb_release -rs)/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb
    rm packages-microsoft-prod.deb
    apt-get update
    apt-get install -y dotnet-sdk-8.0
fi

echo ""
echo "Dependencies installed successfully!"
echo "You can now build with: ./scripts/build-linux.sh"
