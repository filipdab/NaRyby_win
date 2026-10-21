#!/usr/bin/env bash
# Buduje NaRyby.app dla macOS (Apple Silicon i Intel) i pakuje je do dist/mac/*.zip.
# Wymaga .NET 8 SDK:  brew install --cask dotnet-sdk   (albo https://dotnet.microsoft.com/download)
# Użycie:             ./tools/build_mac.sh            – obie architektury
#                     ./tools/build_mac.sh arm64      – tylko Apple Silicon (M1 i nowsze)
#                     ./tools/build_mac.sh x64        – tylko Intel
set -euo pipefail

cd "$(dirname "$0")/.."
PROJECT=NaRyby_mac/NaRyby_mac.csproj
OUT=dist/mac
if [ $# -gt 0 ]; then ARCHS=("$@"); else ARCHS=(arm64 x64); fi

rm -rf "$OUT"
mkdir -p "$OUT"

for ARCH in "${ARCHS[@]}"; do
    RID="osx-$ARCH"
    PUBLISH="$OUT/publish-$ARCH"
    APP="$OUT/$ARCH/NaRyby.app"

    echo "==> $RID"
    dotnet publish "$PROJECT" -c Release -r "$RID" --self-contained true \
        -p:PublishSingleFile=false -p:DebugType=none -o "$PUBLISH"

    mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
    cp -R "$PUBLISH"/. "$APP/Contents/MacOS/"
    cp NaRyby_mac/macos/Info.plist "$APP/Contents/Info.plist"
    cp NaRyby_mac/macos/NaRyby.icns "$APP/Contents/Resources/NaRyby.icns"
    chmod +x "$APP/Contents/MacOS/NaRyby"

    # Na Apple Silicon każdy plik wykonywalny musi mieć przynajmniej podpis ad-hoc.
    if command -v codesign >/dev/null; then
        codesign --force --deep --sign - "$APP"
    else
        echo "   (brak codesign – podpisz aplikację na Macu: codesign --force --deep --sign - NaRyby.app)"
    fi

    ZIP="$PWD/$OUT/NaRyby-mac-$ARCH.zip"
    if command -v ditto >/dev/null; then
        ditto -c -k --keepParent "$APP" "$ZIP"
    else
        (cd "$OUT/$ARCH" && zip -qry "$ZIP" NaRyby.app)
    fi
    rm -rf "$PUBLISH"
    echo "   $APP"
    echo "   $ZIP"
done
