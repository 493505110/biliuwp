using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace scripting
{
    /// <summary>Pure-data port of ScriptDisplay and the Flash geometry helpers.</summary>
    public sealed class M8DisplayApi : IM8ScriptObject
    {
        private const double GradientBoxScale = 1638.4d;
        private readonly IM8RenderHost _renderHost;
        private readonly M8PlayerApi _player;
        private readonly M8ScriptManager _scriptManager;
        private readonly Dictionary<string, object> _defaultConfigData;
        private double _frameRate;

        public M8DisplayApi()
            : this(new M8NullRenderHost())
        {
        }

        public M8DisplayApi(IM8RenderHost renderHost)
            : this(renderHost, (M8PlayerApi)null, (M8ScriptManager)null)
        {
        }

        public M8DisplayApi(IM8RenderHost renderHost, M8ScriptManager scriptManager)
            : this(renderHost, null, scriptManager)
        {
        }

        public M8DisplayApi(IM8RenderHost renderHost, M8PlayerApi player, M8ScriptManager scriptManager)
        {
            this._renderHost = renderHost ?? new M8NullRenderHost();
            this._scriptManager = scriptManager ?? new M8ScriptManager(this._renderHost);
            this._player = player ?? new M8PlayerApi(this._renderHost, this._scriptManager);
            this._player.AttachScriptManager(this._scriptManager);
            this._defaultConfigData = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = 0d,
                ["y"] = 0d,
                ["z"] = null,
                ["scale"] = 1d,
                ["alpha"] = 1d,
                ["parent"] = this._renderHost.Root,
                ["lifeTime"] = 3d,
                ["motion"] = null
            };
        }

        public M8DisplayApi(M8PlayerApi player, IM8RenderHost renderHost, M8ScriptManager scriptManager)
            : this(renderHost, player, scriptManager)
        {
        }

        public M8DisplayApi(IM8RenderHost renderHost, M8ScriptManager scriptManager, M8PlayerApi player)
            : this(renderHost, player, scriptManager)
        {
        }

        public Dictionary<string, object> DefaultConfig
        {
            get { return this._defaultConfigData; }
        }

        public Dictionary<string, object> _defaultConfig
        {
            get { return this._defaultConfigData; }
        }

        public double fullScreenWidth
        {
            get { return this._renderHost.StageWidth; }
        }

        public double fullScreenHeight
        {
            get { return this._renderHost.StageHeight; }
        }

        public double screenWidth
        {
            get { return this._renderHost.StageWidth; }
        }

        public double screenHeight
        {
            get { return this._renderHost.StageHeight; }
        }

        public double stageWidth
        {
            get { return this._renderHost.StageWidth; }
        }

        public double stageHeight
        {
            get { return this._renderHost.StageHeight; }
        }

        public double width
        {
            get { return this._renderHost.StageWidth; }
        }

        public double height
        {
            get { return this._renderHost.StageHeight; }
        }

        public object root
        {
            get { return this._renderHost.Root; }
        }

        public double frameRate
        {
            get { return this._frameRate; }
            set
            {
                if (value > 0 && value < 120) this._frameRate = value;
            }
        }

        public Dictionary<string, object> createMatrix(
            double a = 1d,
            double b = 0d,
            double c = 0d,
            double d = 1d,
            double tx = 0d,
            double ty = 0d)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["a"] = a,
                ["b"] = b,
                ["c"] = c,
                ["d"] = d,
                ["tx"] = tx,
                ["ty"] = ty
            };
        }

        public Dictionary<string, object> createGradientBox(
            double width,
            double height,
            double rotation = 0d,
            double tx = 0d,
            double ty = 0d)
        {
            var scaleX = width / GradientBoxScale;
            var scaleY = height / GradientBoxScale;
            var cos = Math.Cos(rotation);
            var sin = Math.Sin(rotation);
            return this.createMatrix(
                scaleX * cos,
                scaleX * sin,
                -scaleY * sin,
                scaleY * cos,
                tx + width / 2d,
                ty + height / 2d);
        }

        public Dictionary<string, object> createPoint(double x = 0d, double y = 0d)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = x,
                ["y"] = y
            };
        }

        public M8Element createShape(object config = null)
        {
            var parameters = this.PrepareConfig(config);
            var element = new M8Element();
            element.Set("type", "shape");
            this.InitStyle(element, parameters);
            this.SetupMotionElement(parameters, element);
            return element;
        }

        public M8Element createCanvas(object config = null)
        {
            var parameters = this.PrepareConfig(config);
            var element = new M8Element();
            element.Set("type", "canvas");
            this.InitStyle(element, parameters);
            this.SetupMotionElement(parameters, element);
            return element;
        }

        public M8Element createComment(object text, object config = null)
        {
            var parameters = this.PrepareConfig(config);
            Extend(parameters, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["color"] = 16777215d,
                ["font"] = "黑体",
                ["fontsize"] = 25d
            });
            var element = new M8Element();
            element.Set("type", "comment");
            element.Set("text", text);
            this.InitStyle(element, parameters);
            element.Set("text", text);
            this.SetupMotionElement(parameters, element);
            return element;
        }

        public M8Element createButton(object config = null)
        {
            var parameters = this.PrepareConfig(config);
            Extend(parameters, new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["text"] = "Button",
                ["width"] = 60d,
                ["height"] = 30d
            });
            var element = new M8Element();
            element.Set("type", "button");
            this.InitStyle(element, parameters);
            if (Has(parameters, "text")) element.Set("text", Get(parameters, "text"));
            if (Has(parameters, "onclick"))
            {
                var callback = Get(parameters, "onclick");
                element.Set("onclick", callback);
                element.Set("click", (Action)(() => this._player.InvokeCallback(callback)));
                element.Set("invokeClick", (Func<object[], object>)(args =>
                {
                    this._player.InvokeCallback(callback, args ?? new object[0]);
                    return null;
                }));
            }
            this.SetupMotionElement(parameters, element);
            return element;
        }

        public void InvokeButtonClick(M8Element button)
        {
            if (button == null) return;
            this._player.InvokeCallback(button.Get("onclick"));
        }

        public M8Element createTextField(object text = null, object config = null)
        {
            var parameters = this.PrepareConfig(config);
            var element = new M8Element();
            element.Set("type", "textField");
            this.InitStyle(element, parameters);
            if (text != null) element.Set("text", text);
            this.SetupMotionElement(parameters, element);
            return element;
        }

        public Dictionary<string, object> createGlowFilter(
            object color = null,
            double alpha = 1d,
            double blurX = 6d,
            double blurY = 6d,
            double strength = 2d,
            object quality = null,
            bool inner = false,
            bool knockout = false)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["color"] = color ?? 16711680d,
                ["alpha"] = alpha,
                ["blurX"] = blurX,
                ["blurY"] = blurY,
                ["strength"] = strength,
                ["quality"] = quality ?? 1d,
                ["inner"] = inner,
                ["knockout"] = knockout
            };
        }

        public Dictionary<string, object> createBlurFilter(double blurX = 0d, double blurY = 0d, object quality = null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["blurX"] = blurX,
                ["blurY"] = blurY,
                ["quality"] = quality ?? 1d
            };
        }

        public Dictionary<string, object> createBevelFilter(
            double distance = 4d,
            double angle = 45d,
            object highlightColor = null,
            double highlightAlpha = 1d,
            object shadowColor = null,
            double shadowAlpha = 1d,
            double blurX = 4d,
            double blurY = 4d,
            double strength = 1d,
            object quality = null,
            string type = "inner",
            bool knockout = false)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["distance"] = distance,
                ["angle"] = angle,
                ["highlightColor"] = highlightColor ?? 16777215d,
                ["highlightAlpha"] = highlightAlpha,
                ["shadowColor"] = shadowColor ?? 0d,
                ["shadowAlpha"] = shadowAlpha,
                ["blurX"] = blurX,
                ["blurY"] = blurY,
                ["strength"] = strength,
                ["quality"] = quality ?? 1d,
                ["type"] = type,
                ["knockout"] = knockout
            };
        }

        public Dictionary<string, object> createColorMatrixFilter(object matrix = null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["matrix"] = matrix
            };
        }

        public Dictionary<string, object> createConvolutionFilter(
            double matrixX = 0d,
            double matrixY = 0d,
            object matrix = null,
            double divisor = 1d,
            double bias = 0d,
            bool preserveAlpha = true,
            bool clamp = true,
            object color = null,
            double alpha = 0d)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["matrixX"] = matrixX,
                ["matrixY"] = matrixY,
                ["matrix"] = matrix,
                ["divisor"] = divisor,
                ["bias"] = bias,
                ["preserveAlpha"] = preserveAlpha,
                ["clamp"] = clamp,
                ["color"] = color ?? 0d,
                ["alpha"] = alpha
            };
        }

        public Dictionary<string, object> createDisplacementMapFilter(
            object mapBitmap = null,
            object mapPoint = null,
            object componentX = null,
            object componentY = null,
            double scaleX = 0d,
            double scaleY = 0d,
            string mode = "wrap",
            object color = null,
            double alpha = 0d)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["mapBitmap"] = mapBitmap,
                ["mapPoint"] = mapPoint,
                ["componentX"] = componentX ?? 0d,
                ["componentY"] = componentY ?? 0d,
                ["scaleX"] = scaleX,
                ["scaleY"] = scaleY,
                ["mode"] = mode,
                ["color"] = color ?? 0d,
                ["alpha"] = alpha
            };
        }

        public Dictionary<string, object> createDropShadowFilter(
            double distance = 4d,
            double angle = 45d,
            object color = null,
            double alpha = 1d,
            double blurX = 4d,
            double blurY = 4d,
            double strength = 1d,
            object quality = null,
            bool inner = false,
            bool knockout = false,
            bool hideObject = false)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["distance"] = distance,
                ["angle"] = angle,
                ["color"] = color ?? 0d,
                ["alpha"] = alpha,
                ["blurX"] = blurX,
                ["blurY"] = blurY,
                ["strength"] = strength,
                ["quality"] = quality ?? 1d,
                ["inner"] = inner,
                ["knockout"] = knockout,
                ["hideObject"] = hideObject
            };
        }

        public Dictionary<string, object> createGradientBevelFilter(
            double distance = 4d,
            double angle = 45d,
            object colors = null,
            object alphas = null,
            object ratios = null,
            double blurX = 4d,
            double blurY = 4d,
            double strength = 1d,
            object quality = null,
            string type = "inner",
            bool knockout = false)
        {
            return GradientFilter("GradientBevelFilter", distance, angle, colors, alphas, ratios, blurX, blurY, strength, quality, type, knockout);
        }

        public Dictionary<string, object> createGradientGlowFilter(
            double distance = 4d,
            double angle = 45d,
            object colors = null,
            object alphas = null,
            object ratios = null,
            double blurX = 4d,
            double blurY = 4d,
            double strength = 1d,
            object quality = null,
            string type = "inner",
            bool knockout = false)
        {
            return GradientFilter("GradientGlowFilter", distance, angle, colors, alphas, ratios, blurX, blurY, strength, quality, type, knockout);
        }

        public List<object> toIntVector(object values)
        {
            var result = new List<object>();
            foreach (var value in Enumerate(values))
            {
                var number = Number(value);
                if (double.IsNaN(number) || double.IsInfinity(number)) number = 0;
                result.Add((double)unchecked((int)number));
            }
            return result;
        }

        public List<object> toUIntVector(object values)
        {
            var result = new List<object>();
            foreach (var value in Enumerate(values))
            {
                var number = Number(value);
                if (double.IsNaN(number) || double.IsInfinity(number)) number = 0;
                result.Add((double)unchecked((uint)number));
            }
            return result;
        }

        public List<object> toNumberVector(object values)
        {
            var result = new List<object>();
            foreach (var value in Enumerate(values)) result.Add(Number(value));
            return result;
        }

        public Dictionary<string, object> createMatrix3D(object values = null)
        {
            var raw = IdentityRawData();
            var source = values;
            if (source is IDictionary<string, object> dictionary && dictionary.ContainsKey("rawData")) source = dictionary["rawData"];
            var index = 0;
            foreach (var value in Enumerate(source))
            {
                if (index >= 16) break;
                raw[index++] = Number(value);
            }

            return Matrix3DData(raw);
        }

        public Dictionary<string, object> createColorTransform(
            double redMultiplier = 1d,
            double greenMultiplier = 1d,
            double blueMultiplier = 1d,
            double alphaMultiplier = 1d,
            double redOffset = 0d,
            double greenOffset = 0d,
            double blueOffset = 0d,
            double alphaOffset = 0d)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["redMultiplier"] = redMultiplier,
                ["greenMultiplier"] = greenMultiplier,
                ["blueMultiplier"] = blueMultiplier,
                ["alphaMultiplier"] = alphaMultiplier,
                ["redOffset"] = redOffset,
                ["greenOffset"] = greenOffset,
                ["blueOffset"] = blueOffset,
                ["alphaOffset"] = alphaOffset
            };
        }

        public Dictionary<string, object> createTextFormat(
            string font = null,
            object size = null,
            object color = null,
            object bold = null,
            object italic = null,
            object underline = null,
            string url = null,
            string target = null,
            string align = null,
            object leftMargin = null,
            object rightMargin = null,
            object indent = null,
            object leading = null)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["font"] = font,
                ["size"] = size,
                ["color"] = color,
                ["bold"] = bold,
                ["italic"] = italic,
                ["underline"] = underline,
                ["url"] = url,
                ["target"] = target,
                ["align"] = align,
                ["leftMargin"] = leftMargin,
                ["rightMargin"] = rightMargin,
                ["indent"] = indent,
                ["leading"] = leading
            };
        }

        public Dictionary<string, object> createVector3D(double x = 0d, double y = 0d, double z = 0d, double w = 0d)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["x"] = x,
                ["y"] = y,
                ["z"] = z,
                ["w"] = w
            };
        }

        public Dictionary<string, object> pointTowards(object percent, object matrix, object pos, object at = null, object up = null)
        {
            var original = ReadRawData(matrix);
            var position = new[] { original[12], original[13], original[14] };
            var target = new[] { Number(Get(pos, "x")), Number(Get(pos, "y")), Number(Get(pos, "z")) };
            var forward = Normalize(new[] { target[0] - position[0], target[1] - position[1], target[2] - position[2] });
            if (Length(forward) == 0) forward = new[] { 0d, 0d, 1d };
            var localForward = at == null
                ? new[] { 0d, 0d, -1d }
                : new[] { Number(Get(at, "x")), Number(Get(at, "y")), Number(Get(at, "z")) };
            localForward = Normalize(localForward);
            if (Length(localForward) == 0) localForward = new[] { 0d, 0d, -1d };
            var localUp = up == null
                ? new[] { 0d, -1d, 0d }
                : new[] { Number(Get(up, "x")), Number(Get(up, "y")), Number(Get(up, "z")) };
            localUp = Normalize(localUp);
            if (Length(localUp) == 0) localUp = new[] { 0d, -1d, 0d };
            var localRight = Normalize(Cross(localUp, localForward));
            if (Length(localRight) == 0) localRight = new[] { 1d, 0d, 0d };
            localUp = Normalize(Cross(localForward, localRight));

            var worldUp = new[] { 0d, -1d, 0d };
            var worldRight = Normalize(Cross(worldUp, forward));
            if (Length(worldRight) == 0) worldRight = Normalize(Cross(new[] { 0d, 0d, 1d }, forward));
            if (Length(worldRight) == 0) worldRight = new[] { 1d, 0d, 0d };
            worldUp = Normalize(Cross(forward, worldRight));

            var targetMatrix = IdentityRawData();
            var worldBasis = new[] { worldRight, worldUp, forward };
            var localBasis = new[] { localRight, localUp, localForward };
            for (var column = 0; column < 3; column++)
            {
                for (var row = 0; row < 3; row++)
                {
                    targetMatrix[column * 4 + row] =
                        worldBasis[0][row] * localBasis[0][column] +
                        worldBasis[1][row] * localBasis[1][column] +
                        worldBasis[2][row] * localBasis[2][column];
                }
            }
            var amount = Number(percent);
            if (double.IsNaN(amount)) amount = 0;
            var result = new List<double>(original);
            foreach (var index in new[] { 0, 1, 2, 4, 5, 6, 8, 9, 10 })
            {
                result[index] = original[index] + (targetMatrix[index] - original[index]) * amount;
            }
            return Matrix3DData(result);
        }

        public Dictionary<string, object> projectVector(object matrix, object vector)
        {
            var raw = ReadRawData(matrix);
            var x = Number(Get(vector, "x"));
            var y = Number(Get(vector, "y"));
            var z = Number(Get(vector, "z"));
            var projected = Project(raw, x, y, z);
            return this.createVector3D(projected[0], projected[1], projected[2], 0d);
        }

        public void projectVectors(object matrix, object vertices, object projectedVerts, object uvts)
        {
            var raw = ReadRawData(matrix);
            var source = new List<object>(Enumerate(vertices));
            if (source.Count % 3 != 0) return;
            var projected = new List<object>();
            var texture = new List<object>();
            for (var i = 0; i + 2 < source.Count; i += 3)
            {
                var transformed = Transform(raw, Number(source[i]), Number(source[i + 1]), Number(source[i + 2]));
                var w = transformed[3];
                projected.Add(transformed[0] / w);
                projected.Add(transformed[1] / w);
                texture.Add(1d / w);
            }
            AppendValues(projectedVerts, projected);
            SetProjectionDepths(uvts, texture);
        }

        public static void extend(IDictionary<string, object> target, IDictionary<string, object> defaults)
        {
            if (target == null || defaults == null) return;
            foreach (var pair in defaults)
            {
                if (!target.ContainsKey(pair.Key)) target[pair.Key] = pair.Value;
            }
            if (defaults.ContainsKey("motion") && target.ContainsKey("motion") && target["motion"] == null)
            {
                target["motion"] = new Dictionary<string, object>(StringComparer.Ordinal);
            }
        }

        public void InitStyle(M8Element element, IDictionary<string, object> config)
        {
            if (element == null || config == null) return;
            var styleKeys = new[]
            {
                "x", "y", "z", "scale", "scaleX", "scaleY", "rotation", "rotationX", "rotationY",
                "rotationZ", "alpha", "visible", "width", "height", "color", "font", "fontsize", "filters",
                "text", "align", "bold", "italic", "underline", "fillColors", "fillAlphas", "mouseEnabled"
            };
            foreach (var key in styleKeys)
            {
                if (config.ContainsKey(key)) element.Set(key, config[key]);
            }
        }

        public void initStyle(M8Element element, IDictionary<string, object> config)
        {
            this.InitStyle(element, config);
        }

        public object Get(string key)
        {
            if (key == null) return null;
            switch (key)
            {
                case "fullScreenWidth": return this.fullScreenWidth;
                case "fullScreenHeight": return this.fullScreenHeight;
                case "screenWidth": return this.screenWidth;
                case "screenHeight": return this.screenHeight;
                case "stageWidth": return this.stageWidth;
                case "stageHeight": return this.stageHeight;
                case "width": return this.width;
                case "height": return this.height;
                case "root": return this.root;
                case "frameRate": return this.frameRate;
                case "_defaultConfig": return this._defaultConfigData;
                case "createMatrix": return (Func<object[], object>)(args => this.createMatrix(
                    NumberAt(args, 0, 1), NumberAt(args, 1, 0), NumberAt(args, 2, 0), NumberAt(args, 3, 1), NumberAt(args, 4, 0), NumberAt(args, 5, 0)));
                case "createGradientBox": return (Func<object[], object>)(args => this.createGradientBox(
                    NumberAt(args, 0, 0), NumberAt(args, 1, 0), NumberAt(args, 2, 0), NumberAt(args, 3, 0), NumberAt(args, 4, 0)));
                case "createPoint": return (Func<object[], object>)(args => this.createPoint(NumberAt(args, 0, 0), NumberAt(args, 1, 0)));
                case "createShape": return (Func<object[], object>)(args => this.createShape(args.Length > 0 ? args[0] : null));
                case "createCanvas": return (Func<object[], object>)(args => this.createCanvas(args.Length > 0 ? args[0] : null));
                case "createComment": return (Func<object[], object>)(args => this.createComment(args.Length > 0 ? args[0] : null, args.Length > 1 ? args[1] : null));
                case "createButton": return (Func<object[], object>)(args => this.createButton(args.Length > 0 ? args[0] : null));
                case "createTextField": return (Func<object[], object>)(args => this.createTextField(args.Length > 0 ? args[0] : null, args.Length > 1 ? args[1] : null));
                case "createGlowFilter": return (Func<object[], object>)(args => this.createGlowFilter(
                    ValueAt(args, 0), NumberAt(args, 1, 1), NumberAt(args, 2, 6), NumberAt(args, 3, 6), NumberAt(args, 4, 2), ValueAt(args, 5), BoolAt(args, 6, false), BoolAt(args, 7, false)));
                case "createBlurFilter": return (Func<object[], object>)(args => this.createBlurFilter(NumberAt(args, 0, 0), NumberAt(args, 1, 0), ValueAt(args, 2)));
                case "createBevelFilter": return (Func<object[], object>)(args => this.createBevelFilter(
                    NumberAt(args, 0, 4), NumberAt(args, 1, 45), ValueAt(args, 2), NumberAt(args, 3, 1), ValueAt(args, 4), NumberAt(args, 5, 1), NumberAt(args, 6, 4), NumberAt(args, 7, 4), NumberAt(args, 8, 1), ValueAt(args, 9), StringAt(args, 10, "inner"), BoolAt(args, 11, false)));
                case "createColorMatrixFilter": return (Func<object[], object>)(args => this.createColorMatrixFilter(ValueAt(args, 0)));
                case "createConvolutionFilter": return (Func<object[], object>)(args => this.createConvolutionFilter(
                    NumberAt(args, 0, 0), NumberAt(args, 1, 0), ValueAt(args, 2), NumberAt(args, 3, 1), NumberAt(args, 4, 0), BoolAt(args, 5, true), BoolAt(args, 6, true), ValueAt(args, 7), NumberAt(args, 8, 0)));
                case "createDisplacementMapFilter": return (Func<object[], object>)(args => this.createDisplacementMapFilter(
                    ValueAt(args, 0), ValueAt(args, 1), ValueAt(args, 2), ValueAt(args, 3), NumberAt(args, 4, 0), NumberAt(args, 5, 0), StringAt(args, 6, "wrap"), ValueAt(args, 7), NumberAt(args, 8, 0)));
                case "createDropShadowFilter": return (Func<object[], object>)(args => this.createDropShadowFilter(
                    NumberAt(args, 0, 4), NumberAt(args, 1, 45), ValueAt(args, 2), NumberAt(args, 3, 1), NumberAt(args, 4, 4), NumberAt(args, 5, 4), NumberAt(args, 6, 1), ValueAt(args, 7), BoolAt(args, 8, false), BoolAt(args, 9, false), BoolAt(args, 10, false)));
                case "createGradientBevelFilter": return (Func<object[], object>)(args => this.createGradientBevelFilter(
                    NumberAt(args, 0, 4), NumberAt(args, 1, 45), ValueAt(args, 2), ValueAt(args, 3), ValueAt(args, 4), NumberAt(args, 5, 4), NumberAt(args, 6, 4), NumberAt(args, 7, 1), ValueAt(args, 8), StringAt(args, 9, "inner"), BoolAt(args, 10, false)));
                case "createGradientGlowFilter": return (Func<object[], object>)(args => this.createGradientGlowFilter(
                    NumberAt(args, 0, 4), NumberAt(args, 1, 45), ValueAt(args, 2), ValueAt(args, 3), ValueAt(args, 4), NumberAt(args, 5, 4), NumberAt(args, 6, 4), NumberAt(args, 7, 1), ValueAt(args, 8), StringAt(args, 9, "inner"), BoolAt(args, 10, false)));
                case "toIntVector": return (Func<object, object>)(values => this.toIntVector(values));
                case "toUIntVector": return (Func<object, object>)(values => this.toUIntVector(values));
                case "toNumberVector": return (Func<object, object>)(values => this.toNumberVector(values));
                case "createMatrix3D": return (Func<object, object>)(values => this.createMatrix3D(values));
                case "createColorTransform": return (Func<object[], object>)(args => this.createColorTransform(
                    NumberAt(args, 0, 1), NumberAt(args, 1, 1), NumberAt(args, 2, 1), NumberAt(args, 3, 1), NumberAt(args, 4, 0), NumberAt(args, 5, 0), NumberAt(args, 6, 0), NumberAt(args, 7, 0)));
                case "createTextFormat": return (Func<object[], object>)(args => this.createTextFormat(
                    StringAt(args, 0, null), ValueAt(args, 1), ValueAt(args, 2), ValueAt(args, 3), ValueAt(args, 4), ValueAt(args, 5), StringAt(args, 6, null), StringAt(args, 7, null), StringAt(args, 8, null), ValueAt(args, 9), ValueAt(args, 10), ValueAt(args, 11), ValueAt(args, 12)));
                case "createVector3D": return (Func<object[], object>)(args => this.createVector3D(NumberAt(args, 0, 0), NumberAt(args, 1, 0), NumberAt(args, 2, 0), NumberAt(args, 3, 0)));
                case "pointTowards": return (Func<object[], object>)(args => this.pointTowards(ValueAt(args, 0), ValueAt(args, 1), ValueAt(args, 2), ValueAt(args, 3), ValueAt(args, 4)));
                case "projectVector": return (Func<object, object, object>)((matrix, vector) => this.projectVector(matrix, vector));
                case "projectVectors": return (Func<object[], object>)(args =>
                {
                    this.projectVectors(args.Length > 0 ? args[0] : null, args.Length > 1 ? args[1] : null, args.Length > 2 ? args[2] : null, args.Length > 3 ? args[3] : null);
                    return null;
                });
                default: return null;
            }
        }

        public void Set(string key, object value)
        {
            if (key == "frameRate") this.frameRate = Number(value);
        }

        private Dictionary<string, object> PrepareConfig(object config)
        {
            var result = ToDictionary(config);
            Extend(result, this._defaultConfigData);
            return result;
        }

        private void SetupMotionElement(Dictionary<string, object> config, M8Element element)
        {
            var parent = Get(config, "parent");
            if (parent == null) parent = this._renderHost.Root;
            element.Set("parent", parent);

            var motion = element.motionManager;
            motion.SetPlayTime(this._player.stime * 1000d);
            var motionGroup = Get(config, "motionGroup");
            if (motionGroup != null)
            {
                motion.InitTweenGroup(motionGroup, NumberOr(config, "lifeTime", double.NaN));
            }
            else
            {
                var motionConfig = Get(config, "motion");
                if (motionConfig == null) motionConfig = new Dictionary<string, object>(StringComparer.Ordinal);
                var lifeTime = NumberOr(config, "lifeTime", 3d);
                if (double.IsNaN(lifeTime)) lifeTime = 3d;
                if (lifeTime < 0) lifeTime = 0.001d;
                SetIfMissing(motionConfig, "lifeTime", lifeTime);
                motion.InitTween(motionConfig);
            }

            motion.SetCompleteListener(() =>
            {
                var currentParent = element.Get("parent") as M8Element;
                if (currentParent != null) currentParent.RemoveChild(element);
                this._scriptManager.PopEl(element);
            });
            this._scriptManager.PushEl(element);
            this._renderHost.AddElement(element, parent);
            if (this._player.state == PlayerState.PLAYING) motion.Play();
        }

        private static Dictionary<string, object> ToDictionary(object value)
        {
            var result = value as Dictionary<string, object>;
            if (result != null) return result;
            var generic = value as IDictionary<string, object>;
            if (generic != null)
            {
                result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (var pair in generic) result[pair.Key] = pair.Value;
                return result;
            }
            var dictionary = value as IDictionary;
            if (dictionary != null)
            {
                result = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (DictionaryEntry pair in dictionary)
                {
                    if (pair.Key != null) result[VirtualMachine.As3String(pair.Key)] = pair.Value;
                }
                return result;
            }
            return new Dictionary<string, object>(StringComparer.Ordinal);
        }

        private static void SetIfMissing(object target, string key, object value)
        {
            var dictionary = target as IDictionary<string, object>;
            if (dictionary != null)
            {
                var current = dictionary.ContainsKey(key) ? dictionary[key] : null;
                if (!dictionary.ContainsKey(key) || current == null || (current is double && double.IsNaN((double)current))) dictionary[key] = value;
                return;
            }
            VirtualMachine.SetMember(target, key, value);
        }

        private static void Extend(IDictionary<string, object> target, IDictionary<string, object> defaults)
        {
            if (target == null || defaults == null) return;
            foreach (var pair in defaults)
            {
                if (!target.ContainsKey(pair.Key)) target[pair.Key] = pair.Value;
            }
            if (defaults.ContainsKey("motion") && target.ContainsKey("motion") && target["motion"] == null)
            {
                target["motion"] = new Dictionary<string, object>(StringComparer.Ordinal);
            }
        }

        private static Dictionary<string, object> GradientFilter(string name, double distance, double angle, object colors, object alphas, object ratios, double blurX, double blurY, double strength, object quality, string type, bool knockout)
        {
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["distance"] = distance,
                ["angle"] = angle,
                ["colors"] = colors,
                ["alphas"] = alphas,
                ["ratios"] = ratios,
                ["blurX"] = blurX,
                ["blurY"] = blurY,
                ["strength"] = strength,
                ["quality"] = quality ?? 1d,
                ["type"] = type,
                ["knockout"] = knockout
            };
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            var enumerable = value as IEnumerable;
            if (enumerable == null || value is string) yield break;
            foreach (var item in enumerable) yield return item;
        }

        private static List<double> IdentityRawData()
        {
            return new List<double>
            {
                1d, 0d, 0d, 0d,
                0d, 1d, 0d, 0d,
                0d, 0d, 1d, 0d,
                0d, 0d, 0d, 1d
            };
        }

        private static Dictionary<string, object> Matrix3DData(IList<double> raw)
        {
            var values = new List<object>(16);
            for (var i = 0; i < 16; i++) values.Add(i < raw.Count ? raw[i] : 0d);
            var result = new Dictionary<string, object>(StringComparer.Ordinal) { ["rawData"] = values };
            for (var row = 0; row < 4; row++)
            {
                for (var column = 0; column < 4; column++)
                {
                    result["m" + row + column] = values[column * 4 + row];
                }
            }
            return result;
        }

        private static List<double> ReadRawData(object matrix)
        {
            var result = IdentityRawData();
            object values = matrix;
            if (matrix != null)
            {
                var raw = Get(matrix, "rawData");
                if (raw != null) values = raw;
            }
            var index = 0;
            foreach (var value in Enumerate(values))
            {
                if (index >= 16) break;
                result[index++] = Number(value);
            }
            return result;
        }

        private static double[] Transform(IList<double> matrix, double x, double y, double z)
        {
            return new[]
            {
                matrix[0] * x + matrix[4] * y + matrix[8] * z + matrix[12],
                matrix[1] * x + matrix[5] * y + matrix[9] * z + matrix[13],
                matrix[2] * x + matrix[6] * y + matrix[10] * z + matrix[14],
                matrix[3] * x + matrix[7] * y + matrix[11] * z + matrix[15]
            };
        }

        private static double[] Project(IList<double> matrix, double x, double y, double z)
        {
            var transformed = Transform(matrix, x, y, z);
            var w = transformed[3];
            transformed[0] /= w;
            transformed[1] /= w;
            transformed[2] /= w;
            return transformed;
        }

        private static void AppendValues(object destination, IList<object> values)
        {
            var list = destination as IList<object>;
            if (list != null)
            {
                foreach (var value in values) list.Add(value);
                return;
            }
            var nonGeneric = destination as IList;
            if (nonGeneric != null)
            {
                foreach (var value in values) nonGeneric.Add(value);
            }
        }

        private static void SetProjectionDepths(object destination, IList<object> depths)
        {
            var list = destination as IList<object>;
            if (list != null)
            {
                for (var i = 0; i < depths.Count; i++)
                {
                    var index = i * 3 + 2;
                    while (list.Count <= index) list.Add(null);
                    list[index] = depths[i];
                }
                return;
            }
            var nonGeneric = destination as IList;
            if (nonGeneric != null)
            {
                for (var i = 0; i < depths.Count; i++)
                {
                    var index = i * 3 + 2;
                    while (nonGeneric.Count <= index) nonGeneric.Add(null);
                    nonGeneric[index] = depths[i];
                }
            }
        }

        private static double[] Cross(double[] a, double[] b)
        {
            return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] };
        }

        private static double[] Normalize(double[] value)
        {
            var length = Length(value);
            if (length == 0) return new[] { 0d, 0d, 0d };
            return new[] { value[0] / length, value[1] / length, value[2] / length };
        }

        private static double Length(double[] value)
        {
            return Math.Sqrt(value[0] * value[0] + value[1] * value[1] + value[2] * value[2]);
        }

        private static object Get(object value, string key)
        {
            if (value == null) return null;
            if (value is IM8ScriptObject scriptObject) return scriptObject.Get(key);
            if (value is IDictionary<string, object> generic && generic.TryGetValue(key, out var result)) return result;
            if (value is IDictionary dictionary && dictionary.Contains(key)) return dictionary[key];
            return VirtualMachine.GetMember(value, key);
        }

        private static bool Has(IDictionary<string, object> value, string key)
        {
            return value != null && value.ContainsKey(key);
        }

        private static double Number(object value)
        {
            var number = VirtualMachine.ToNumber(value);
            if (!double.IsNaN(number)) return number;
            if (value is IConvertible)
            {
                try { return Convert.ToDouble(value, CultureInfo.InvariantCulture); }
                catch { }
            }
            return double.NaN;
        }

        private static double NumberOr(IDictionary<string, object> value, string key, double fallback)
        {
            if (!value.ContainsKey(key)) return fallback;
            var number = Number(value[key]);
            return double.IsNaN(number) ? fallback : number;
        }

        private static double NumberAt(object[] values, int index, double fallback)
        {
            if (values == null || index >= values.Length || values[index] == null) return fallback;
            var number = Number(values[index]);
            return double.IsNaN(number) ? fallback : number;
        }

        private static object ValueAt(object[] values, int index)
        {
            return values != null && index < values.Length ? values[index] : null;
        }

        private static bool BoolAt(object[] values, int index, bool fallback)
        {
            var value = ValueAt(values, index);
            return value == null ? fallback : VirtualMachine.Truthy(value);
        }

        private static string StringAt(object[] values, int index, string fallback)
        {
            var value = ValueAt(values, index);
            return value == null ? fallback : VirtualMachine.As3String(value);
        }
    }
}
