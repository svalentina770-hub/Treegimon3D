mergeInto(LibraryManager.library, {
    GetLocalStorageItem: function (keyPtr) {
        var key = UTF8ToString(keyPtr);
        var value = "";

        try {
            var storedValue = window.localStorage.getItem(key);

            if (storedValue === null || storedValue === undefined) {
                console.warn("[LocalStorageBridge] No existe la clave en localStorage:", key);
                value = "";
            } else {
                value = storedValue;
            }
        } catch (error) {
            console.error("[LocalStorageBridge] Error leyendo localStorage:", error);
            value = "";
        }

        var lengthBytes = lengthBytesUTF8(value) + 1;
        var stringOnWasmHeap = _malloc(lengthBytes);
        stringToUTF8(value, stringOnWasmHeap, lengthBytes);
        return stringOnWasmHeap;
    },

    SetLocalStorageItem: function (keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);

        try {
            window.localStorage.setItem(key, value);
            console.log("[LocalStorageBridge] localStorage actualizado:", key);
        } catch (error) {
            console.error("[LocalStorageBridge] Error escribiendo localStorage:", error);
        }
    },

    HasLocalStorageItem: function (keyPtr) {
        var key = UTF8ToString(keyPtr);

        try {
            return window.localStorage.getItem(key) !== null ? 1 : 0;
        } catch (error) {
            console.error("[LocalStorageBridge] Error verificando localStorage:", error);
            return 0;
        }
    },

    RemoveLocalStorageItem: function (keyPtr) {
        var key = UTF8ToString(keyPtr);

        try {
            window.localStorage.removeItem(key);
            console.log("[LocalStorageBridge] localStorage eliminado:", key);
        } catch (error) {
            console.error("[LocalStorageBridge] Error eliminando localStorage:", error);
        }
    }
});