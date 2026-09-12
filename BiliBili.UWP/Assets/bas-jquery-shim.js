(function (global) {
    "use strict";

    function isPlainObject(value) {
        if (!value || Object.prototype.toString.call(value) !== "[object Object]") {
            return false;
        }

        var prototype = Object.getPrototypeOf(value);
        return prototype === Object.prototype || prototype === null;
    }

    function dollar(value) {
        if (typeof value === "string") {
            var template = document.createElement("template");
            template.innerHTML = value.trim();
            return Array.prototype.slice.call(template.content.childNodes);
        }

        if (value == null) {
            return [];
        }

        if (typeof value.length === "number" && typeof value !== "function") {
            return Array.prototype.slice.call(value);
        }

        return [value];
    }

    dollar.extend = function () {
        var deep = false;
        var target;
        var sourceIndex = 0;
        if (typeof arguments[0] === "boolean") {
            deep = arguments[0];
            sourceIndex++;
        }

        target = arguments[sourceIndex] || {};
        sourceIndex++;
        if (typeof target !== "object" && typeof target !== "function") {
            target = {};
        }

        for (; sourceIndex < arguments.length; sourceIndex++) {
            var source = arguments[sourceIndex];
            if (source == null) {
                continue;
            }

            Object.keys(source).forEach(function (key) {
                if (key === "__proto__") {
                    return;
                }

                var copy = source[key];
                if (target === copy) {
                    return;
                }

                if (deep && (Array.isArray(copy) || isPlainObject(copy))) {
                    var clone = Array.isArray(copy)
                        ? (Array.isArray(target[key]) ? target[key] : [])
                        : (isPlainObject(target[key]) ? target[key] : {});
                    target[key] = dollar.extend(true, clone, copy);
                } else if (copy !== undefined) {
                    target[key] = copy;
                }
            });
        }

        return target;
    };

    global.$ = dollar;
})(window);
