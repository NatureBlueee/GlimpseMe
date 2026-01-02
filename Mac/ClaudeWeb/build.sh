#!/bin/bash

# GlimpseMe for macOS - 编译脚本

echo "🔨 Building GlimpseMe for macOS..."

# 创建 build 目录
mkdir -p build

# 编译（使用 swiftc）
swiftc \
    -o build/GlimpseMac \
    -framework Cocoa \
    -framework ApplicationServices \
    Sources/*.swift

if [ $? -eq 0 ]; then
    echo "✅ Build successful!"
    echo "📦 Executable: build/GlimpseMac"
    echo ""
    echo "运行方式："
    echo "  ./build/GlimpseMac"
    echo ""
    echo "⚠️  首次运行会请求辅助功能权限"
else
    echo "❌ Build failed"
    exit 1
fi
