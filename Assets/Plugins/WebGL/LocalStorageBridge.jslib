mergeInto(LibraryManager.library, {
    GetLocalStorageItem: function (keyPtr) {
        var key = UTF8ToString(keyPtr);

        try {
            var value = window.localStorage.getItem(key);

            if (value === null || value === undefined) {
                console.warn("[LocalStorageBridge] No existe la clave en localStorage:", key);
                return stringToNewUTF8("");
            }

            return stringToNewUTF8(value);
        } catch (error) {
            console.error("[LocalStorageBridge] Error leyendo localStorage:", error);
            return stringToNewUTF8("");
        }
    },

    SetLocalStorageItem: function (keyPtr, valuePtr) {
        var key = UTF8ToString(keyPtr);
        var value = UTF8ToString(valuePtr);

        try {
            window.localStorage.setItem(key, value);
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
        } catch (error) {
            console.error("[LocalStorageBridge] Error eliminando localStorage:", error);
        }
    }
});