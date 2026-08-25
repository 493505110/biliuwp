using System;
using System.Collections.Generic;

namespace scripting
{
    /// <summary>Object boundary used by the M8 virtual machine.</summary>
    public interface IM8ScriptObject
    {
        object Get(string key);
        void Set(string key, object value);
    }

    /// <summary>Pure data representation of a display element exposed to M8 scripts.</summary>
    public sealed class M8Element : IM8ScriptObject
    {
        private readonly Dictionary<string, object> _properties;

        public M8Element()
        {
            this._properties = new Dictionary<string, object>(StringComparer.Ordinal);
            this._properties["x"] = 0d;
            this._properties["y"] = 0d;
            this._properties["scaleX"] = 1d;
            this._properties["scaleY"] = 1d;
            this._properties["rotation"] = 0d;
            this._properties["rotationX"] = 0d;
            this._properties["rotationY"] = 0d;
            this._properties["rotationZ"] = 0d;
            this._properties["alpha"] = 1d;
            this._properties["visible"] = true;
            this._properties["width"] = 0d;
            this._properties["height"] = 0d;
            this._properties["fontsize"] = 25d;
            this._properties["text"] = string.Empty;
            this._properties["color"] = 16777215d;
            this._properties["type"] = null;
            this._properties["parent"] = null;
            this._properties["children"] = new List<object>();
            this._properties["filters"] = new List<object>();
            this._properties["position"] = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = 0d,
                ["y"] = 0d
            };
            this.motionManager = new M8Motion(this);
        }

        /// <summary>Backing script properties. The VM normally uses Get and Set.</summary>
        public Dictionary<string, object> Properties
        {
            get { return this._properties; }
        }

        /// <summary>Motion manager associated with this element.</summary>
        public M8Motion motionManager { get; set; }

        public object this[string key]
        {
            get { return this.Get(key); }
            set { this.Set(key, value); }
        }

        public object Get(string key)
        {
            if (key == "motionManager") return this.motionManager;
            if (key == "scale") return this._properties["scaleX"];
            if (key == null) return null;
            return this._properties.TryGetValue(key, out var value) ? value : null;
        }

        public void Set(string key, object value)
        {
            if (key == null) return;
            if (key == "motionManager")
            {
                this.motionManager = value as M8Motion;
                return;
            }

            switch (key)
            {
                case "x":
                case "y":
                case "scaleX":
                case "scaleY":
                case "rotationX":
                case "rotationY":
                case "rotationZ":
                case "alpha":
                case "width":
                case "height":
                case "fontsize":
                case "color":
                    this._properties[key] = VirtualMachine.ToNumber(value);
                    if (key == "rotationZ") this._properties["rotation"] = this._properties[key];
                    this.UpdatePosition(key);
                    return;
                case "rotation":
                    this._properties["rotation"] = VirtualMachine.ToNumber(value);
                    this._properties["rotationZ"] = this._properties["rotation"];
                    return;
                case "scale":
                {
                    var scale = VirtualMachine.ToNumber(value);
                    this._properties["scaleX"] = scale;
                    this._properties["scaleY"] = scale;
                    return;
                }
                case "visible":
                    this._properties[key] = VirtualMachine.Truthy(value);
                    return;
                case "text":
                    this._properties[key] = value == null ? string.Empty : VirtualMachine.As3String(value);
                    return;
                case "parent":
                case "type":
                case "children":
                case "filters":
                case "position":
                    this._properties[key] = value;
                    this.UpdatePosition(key);
                    return;
                default:
                    this._properties[key] = value;
                    return;
            }
        }

        public double x
        {
            get { return GetNumber("x"); }
            set { this.Set("x", value); }
        }

        public double y
        {
            get { return GetNumber("y"); }
            set { this.Set("y", value); }
        }

        public double scaleX
        {
            get { return GetNumber("scaleX"); }
            set { this.Set("scaleX", value); }
        }

        public double scaleY
        {
            get { return GetNumber("scaleY"); }
            set { this.Set("scaleY", value); }
        }

        public double rotation
        {
            get { return GetNumber("rotation"); }
            set { this.Set("rotation", value); }
        }

        public double rotationX
        {
            get { return GetNumber("rotationX"); }
            set { this.Set("rotationX", value); }
        }

        public double rotationY
        {
            get { return GetNumber("rotationY"); }
            set { this.Set("rotationY", value); }
        }

        public double rotationZ
        {
            get { return GetNumber("rotationZ"); }
            set { this.Set("rotationZ", value); }
        }

        public double alpha
        {
            get { return GetNumber("alpha"); }
            set { this.Set("alpha", value); }
        }

        public bool visible
        {
            get { return this._properties.TryGetValue("visible", out var value) && VirtualMachine.Truthy(value); }
            set { this.Set("visible", value); }
        }

        public double width
        {
            get { return GetNumber("width"); }
            set { this.Set("width", value); }
        }

        public double height
        {
            get { return GetNumber("height"); }
            set { this.Set("height", value); }
        }

        public double fontsize
        {
            get { return GetNumber("fontsize"); }
            set { this.Set("fontsize", value); }
        }

        public string text
        {
            get { return this._properties["text"] as string ?? string.Empty; }
            set { this.Set("text", value); }
        }

        public double color
        {
            get { return GetNumber("color"); }
            set { this.Set("color", value); }
        }

        public object type
        {
            get { return this.Get("type"); }
            set { this.Set("type", value); }
        }

        public object parent
        {
            get { return this.Get("parent"); }
            set { this.Set("parent", value); }
        }

        public List<object> children
        {
            get { return this._properties["children"] as List<object>; }
            set { this.Set("children", value); }
        }

        public List<object> filters
        {
            get { return this._properties["filters"] as List<object>; }
            set { this.Set("filters", value); }
        }

        public object position
        {
            get { return this.Get("position"); }
            set { this.Set("position", value); }
        }

        public void AddChild(object child)
        {
            if (child == null) return;
            if (this.children == null) this._properties["children"] = new List<object>();
            if (!this.children.Contains(child)) this.children.Add(child);
            if (child is M8Element element) element.parent = this;
        }

        public void RemoveChild(object child)
        {
            if (child == null) return;
            if (this.children != null) this.children.Remove(child);
            if (child is M8Element element && ReferenceEquals(element.parent, this)) element.parent = null;
        }

        private double GetNumber(string key)
        {
            return VirtualMachine.ToNumber(this.Get(key));
        }

        private void UpdatePosition(string key)
        {
            if (key != "x" && key != "y" && key != "position") return;
            if (key == "x" || key == "y")
            {
                if (this._properties["position"] is Dictionary<string, object> point)
                {
                    point[key] = this._properties[key];
                }
                else if (this._properties["position"] is IM8ScriptObject scriptPoint)
                {
                    scriptPoint.Set(key, this._properties[key]);
                }
                return;
            }
            if (key == "position")
            {
                if (this._properties["position"] is IM8ScriptObject scriptPoint)
                {
                    this._properties["x"] = VirtualMachine.ToNumber(scriptPoint.Get("x"));
                    this._properties["y"] = VirtualMachine.ToNumber(scriptPoint.Get("y"));
                }
                else if (this._properties["position"] is Dictionary<string, object> newPoint)
                {
                    if (newPoint.TryGetValue("x", out var px)) this._properties["x"] = VirtualMachine.ToNumber(px);
                    if (newPoint.TryGetValue("y", out var py)) this._properties["y"] = VirtualMachine.ToNumber(py);
                }
            }
        }
    }
}
