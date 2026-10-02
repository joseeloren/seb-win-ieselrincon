// Compatibility with Isard's Vue RDP viewer. Guacamole remains responsible for
// translating ordinary keys; SEB only reconciles modifiers and isolated clipboard.
(function () {
    if (SafeExamBrowser.remoteDesktop) return;
    var active = false;
    var previous = null;
    var modifiers = [
        ['ctrlKey', 0xffe3, 0xffe4], ['altKey', 0xffe9, 0xffea],
        ['shiftKey', 0xffe1, 0xffe2], ['metaKey', 0xffeb, 0xffec]
    ];
    function findViewer() {
        var roots = document.querySelectorAll('#app, [data-v-app]');
        var queue = Array.from(roots, function (root) { return root.__vue__; }).filter(Boolean);
        var seen = new Set();
        while (queue.length && seen.size < 256) {
            var component = queue.shift();
            if (!component || seen.has(component)) continue;
            seen.add(component);
            if (component.$options && component.$options.name === 'Rdp' && component.keyboard &&
                component.client && component.inputSink && typeof component.client.sendKeyEvent === 'function') return component;
            if (component.$children) queue.push.apply(queue, component.$children);
        }
        return null;
    }
    function isInput(viewer, target) {
        var sink = viewer && viewer.inputSink.getElement();
        return !!(sink && (sink === target || sink.contains(target)));
    }
    function notify(value) {
        if (active !== value) {
            active = value;
            CefSharp.PostMessage({ Type: 'RemoteDesktopKeyboard', Active: value });
        }
    }
    function diagnose(name, viewer, event, key) {
        var pressed = viewer && viewer.keyboard.pressed || {};
        CefSharp.PostMessage({ Type: 'RemoteDesktopKeyboardDiagnostic', Event: name, Key: key || 'None',
            Viewer: !!viewer, Focused: !!(event && isInput(viewer, event.target)),
            Ctrl: !!(event && event.ctrlKey), Alt: !!(event && event.altKey),
            Shift: !!(event && event.shiftKey), Meta: !!(event && event.metaKey),
            RemoteCtrl: !!(pressed[0xffe3] || pressed[0xffe4]),
            RemoteAlt: !!(pressed[0xffe9] || pressed[0xffea]),
            RemoteShift: !!(pressed[0xffe1] || pressed[0xffe2]),
            RemoteMeta: !!(pressed[0xffeb] || pressed[0xffec]) });
    }
    function diagnosticKey(event) {
        if (['Control', 'Alt', 'Shift', 'Meta'].indexOf(event.key) !== -1) return event.key;
        // Only shortcut categories are recorded, never typed text or clipboard data.
        if (event.ctrlKey && ['KeyA', 'KeyC', 'KeyV', 'KeyX'].indexOf(event.code) !== -1) return event.code;
        return null;
    }
    function reset(viewer) {
        if (!viewer) return;
        viewer.keyboard.reset();
        modifiers.forEach(function (entry) {
            viewer.client.sendKeyEvent(0, entry[1]);
            viewer.client.sendKeyEvent(0, entry[2]);
        });
    }
    function synchronize(viewer, event) {
        modifiers.forEach(function (entry) {
            var keyboard = viewer.keyboard;
            if (!event[entry[0]]) {
                // Also clear remote state not represented in Guacamole.pressed.
                keyboard.release(entry[1]); keyboard.release(entry[2]);
                viewer.client.sendKeyEvent(0, entry[1]); viewer.client.sendKeyEvent(0, entry[2]);
            } else if (!keyboard.pressed[entry[1]] && !keyboard.pressed[entry[2]]) {
                // Focus can arrive while Ctrl is already held, without a keydown.
                keyboard.press(entry[1]);
            }
        });
    }
    function isolated() { return SafeExamBrowser.clipboard; }
    function sendClipboard(viewer) {
        var buffer = isolated();
        if (!buffer || typeof viewer.client.createClipboardStream !== 'function') return;
        var stream = viewer.client.createClipboardStream('text/plain');
        // Guacamole blobs are bounded. Each base64 chunk decodes to <= 6 KiB.
        var encoded = buffer.getContentEncoded();
        for (var offset = 0; offset < encoded.length; offset += 8192) stream.sendBlob(encoded.slice(offset, offset + 8192));
        stream.sendEnd();
    }
    SafeExamBrowser.remoteDesktop = {
        isInput: function (target) { return isInput(findViewer(), target); },
        paste: function (target) { var viewer = findViewer(); if (isInput(viewer, target)) sendClipboard(viewer); }
    };
    document.addEventListener('keydown', function (event) {
        var viewer = findViewer();
        var focused = isInput(viewer, event.target);
        notify(focused);
        if (!focused) return;
        var key = diagnosticKey(event);
        if (key) diagnose('KeyDownBefore', viewer, event, key);
        synchronize(viewer, event);
        if (key) diagnose('KeyDownAfter', viewer, event, key);
        if (event.ctrlKey && !event.altKey && !event.metaKey && (event.code === 'KeyV' || (event.key || '').toLowerCase() === 'v')) sendClipboard(viewer);
    }, true);
    document.addEventListener('focusin', function (event) {
        var viewer = findViewer();
        var focused = isInput(viewer, event.target);
        diagnose('FocusInBefore', viewer, event);
        // The password dialog and the viewer can exchange focus without a
        // window blur. Discard remote modifiers from the previous interaction;
        // the next keydown restores physically held modifiers from its flags.
        if (focused && !active) reset(viewer);
        notify(focused);
        diagnose('FocusInAfter', viewer, event);
    }, true);
    document.addEventListener('focusout', function (event) {
        var viewer = findViewer();
        if (isInput(viewer, event.target) && !isInput(viewer, event.relatedTarget)) {
            diagnose('FocusOutBefore', viewer, event);
            reset(viewer);
            notify(false);
            diagnose('FocusOutAfter', viewer, event);
        }
    }, true);
    document.addEventListener('keyup', function (event) {
        var viewer = findViewer();
        var key = diagnosticKey(event);
        if (key && isInput(viewer, event.target)) diagnose('KeyUp', viewer, event, key);
    }, true);
    function leave() {
        var viewer = findViewer();
        diagnose('LeaveBefore', viewer);
        reset(viewer); notify(false);
        diagnose('LeaveAfter', viewer);
    }
    window.addEventListener('blur', leave);
    document.addEventListener('visibilitychange', function () { if (document.visibilityState !== 'visible') leave(); });
    window.addEventListener('pagehide', leave);
    var timer = window.setInterval(function () {
        var viewer = findViewer();
        if ((viewer && (!previous || viewer.keyboard !== previous.keyboard || viewer.client !== previous.client)) || (!viewer && previous)) {
            diagnose('ViewerChanged', viewer);
            reset(previous); reset(viewer);
            previous = viewer ? { keyboard: viewer.keyboard, client: viewer.client } : null;
        }
        notify(document.visibilityState === 'visible' && document.hasFocus() && isInput(viewer, document.activeElement));
    }, 500);
    window.addEventListener('pagehide', function () { window.clearInterval(timer); });
    // Isard uses Clipboard API to synchronize the RDP clipboard. Keep that
    // exchange inside SEB's isolated buffer instead of the host OS clipboard.
    if (navigator.clipboard && typeof navigator.clipboard.readText === 'function' && typeof navigator.clipboard.writeText === 'function') {
        var originalRead = navigator.clipboard.readText.bind(navigator.clipboard);
        var originalWrite = navigator.clipboard.writeText.bind(navigator.clipboard);
        navigator.clipboard.readText = function () {
            return findViewer() && isolated() ? Promise.resolve(isolated().text) : originalRead();
        };
        navigator.clipboard.writeText = function (value) {
            if (!findViewer() || !isolated()) return originalWrite(value);
            var buffer = isolated();
            buffer.clear(); buffer.text = String(value);
            CefSharp.PostMessage({ Type: 'Clipboard', Id: buffer.id, Content: buffer.getContentEncoded() });
            return Promise.resolve();
        };
    }
})();
