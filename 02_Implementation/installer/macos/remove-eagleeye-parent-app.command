#!/bin/zsh
# EagleEye Parent App - Entfernen / Removal (US-007 AC-6, ADR-020).
# Removes everything the app left on this Mac, after the app was moved to the Trash.
# Uses only macOS tools, no sudo. Touches only paths that contain "com.adlemich.eagleeye" or the folders
# "Application Support/EagleEye" and "Application Support/EagleEye-Debug". It never deletes the app itself and
# never edits the Dock settings.
# Option: --dry-run lists what would be removed, without removing anything (test aid).

emulate -L zsh
setopt NO_NOMATCH

BUNDLE_ID="com.adlemich.eagleeye"
APP_PATH="/Applications/EagleEye Parent App.app"
DRY_RUN=0
[[ "$1" == "--dry-run" ]] && DRY_RUN=1
REMOVED=0

say() { print -r -- "$1"; }

say "EagleEye Parent App - Entfernen / Removal"
say "=========================================="
[[ $DRY_RUN -eq 1 ]] && say "(Testlauf, es wird nichts gelöscht / dry run, nothing is removed)"
say ""

# 1. The app must not be running.
if pgrep -f "EagleEye Parent App.app/Contents/MacOS/" >/dev/null 2>&1; then
    say "Bitte zuerst die App beenden (EagleEye Parent App > Beenden, oder ⌘Q)."
    say "Please quit the app first (EagleEye Parent App > Quit, or ⌘Q)."
    exit 1
fi

# 2. The app must be in the Trash already (the script never deletes the app itself).
if [[ -d "$APP_PATH" ]]; then
    say "Die App liegt noch im Ordner Programme. Bitte zuerst \"EagleEye Parent App\" in den Papierkorb legen."
    say "The app is still in the Applications folder. Please move \"EagleEye Parent App\" to the Trash first."
    exit 1
fi

removed() {
    REMOVED=$((REMOVED + 1))
    if [[ $DRY_RUN -eq 1 ]]; then
        say "  würde entfernt / would remove: $1"
    else
        say "  entfernt / removed: $1"
    fi
}

remove_path() {
    local target="$1"
    if [[ -e "$target" || -L "$target" ]]; then
        [[ $DRY_RUN -eq 0 ]] && rm -rf -- "$target"
        removed "$target"
    fi
}

remove_keychain_item() {
    local service="$1"
    while security find-generic-password -s "$service" -a "pairing-token" >/dev/null 2>&1; do
        if [[ $DRY_RUN -eq 1 ]]; then
            removed "Schlüsselbund / Keychain: $service (pairing-token)"
            return
        fi
        security delete-generic-password -s "$service" -a "pairing-token" >/dev/null 2>&1 || return
        removed "Schlüsselbund / Keychain: $service (pairing-token)"
    done
}

# 3. Data, preferences, caches, Keychain items.
remove_path "$HOME/Library/Application Support/EagleEye"
remove_path "$HOME/Library/Application Support/EagleEye-Debug"

if defaults read "$BUNDLE_ID" >/dev/null 2>&1; then
    [[ $DRY_RUN -eq 0 ]] && defaults delete "$BUNDLE_ID" >/dev/null 2>&1
    removed "Einstellungen / Preferences: $BUNDLE_ID"
fi
remove_path "$HOME/Library/Preferences/$BUNDLE_ID.plist"
remove_path "$HOME/Library/Caches/$BUNDLE_ID"
remove_path "$HOME/Library/HTTPStorages/$BUNDLE_ID"
remove_path "$HOME/Library/HTTPStorages/$BUNDLE_ID.binarycookies"
remove_path "$HOME/Library/Saved Application State/$BUNDLE_ID.savedState"
remove_path "$HOME/Library/WebKit/$BUNDLE_ID"
remove_path "$HOME/Library/Containers/$BUNDLE_ID"
remove_path "$HOME/Library/Application Scripts/$BUNDLE_ID"

remove_keychain_item "$BUNDLE_ID"
remove_keychain_item "$BUNDLE_ID.debug"

# Privacy settings of the app (best effort; Local Network permissions are not reset by tccutil).
[[ $DRY_RUN -eq 0 ]] && tccutil reset All "$BUNDLE_ID" >/dev/null 2>&1

# 4. Read-only Dock check (the script does not edit the Dock settings).
if defaults read com.apple.dock persistent-apps 2>/dev/null | grep -q "$BUNDLE_ID"; then
    say ""
    say "Hinweis: Das Dock enthält noch ein Symbol der App. Rechtsklick auf das Symbol > Optionen > Aus dem Dock entfernen."
    say "Note: the Dock still shows the app. Right-click the icon > Options > Remove from Dock."
fi

# 5. Summary.
say ""
if [[ $REMOVED -eq 0 ]]; then
    say "Nichts zu entfernen. / Nothing to remove."
elif [[ $DRY_RUN -eq 1 ]]; then
    say "Würde entfernen: $REMOVED Einträge. / Would remove: $REMOVED items."
else
    say "Entfernt: $REMOVED Einträge. / Removed: $REMOVED items."
fi
exit 0
