/*! DECORATOR - Main Script
 * execute main composition
 */

var Akari = Global._get("__akari");

Akari.stop();

Akari.Utilities.Factory.extend(this, Akari.Utilities);
Factory.extend(this, Akari.Display);
Factory.extend(this, Akari.Display.Text);
Factory.extend(this, Akari.Display.Effects);
Factory.extend(this, Akari.Animation);

var Decorator = {};
var Generator = {};
var materialColors = {
    "Others": {"Black": 0, "White": 16777215},
    "Red": {
        "100": 16764370,
        "200": 15702682,
        "300": 15037299,
        "400": 15684432,
        "500": 16007990,
        "600": 15022389,
        "700": 13840175,
        "800": 12986408,
        "900": 12000284,
        "Red": 16007990,
        "050": 16772078,
        "A100": 16747136,
        "A200": 16732754,
        "A400": 16717636,
        "A700": 13959168
    },
    "Pink": {
        "100": 16301008,
        "200": 16027569,
        "300": 15753874,
        "400": 15483002,
        "500": 15277667,
        "600": 14162784,
        "700": 12720219,
        "800": 11342935,
        "900": 8916559,
        "Pink": 15277667,
        "050": 16573676,
        "A100": 16744619,
        "A200": 16728193,
        "A400": 16056407,
        "A700": 12915042
    },
    "Purple": {
        "100": 14794471,
        "200": 13538264,
        "300": 12216520,
        "400": 11225020,
        "500": 10233776,
        "600": 9315498,
        "700": 8069026,
        "800": 6953882,
        "900": 4854924,
        "Purple": 10233776,
        "050": 15984117,
        "A100": 15368444,
        "A200": 14696699,
        "A400": 13959417,
        "A700": 11141375
    },
    "DeepPurple": {
        "100": 13747433,
        "200": 11771355,
        "300": 9795021,
        "400": 8280002,
        "500": 6765239,
        "600": 6174129,
        "700": 5320104,
        "800": 4532128,
        "900": 3218322,
        "Deep Purple": 6765239,
        "050": 15591414,
        "A100": 11766015,
        "A200": 8146431,
        "A400": 6627327,
        "A700": 6422762
    },
    "Indigo": {
        "100": 12962537,
        "200": 10463450,
        "300": 7964363,
        "400": 6056896,
        "500": 4149685,
        "600": 3754411,
        "700": 3162015,
        "800": 2635155,
        "900": 1713022,
        "Indigo": 4149685,
        "050": 15264502,
        "A100": 9215743,
        "A200": 5467646,
        "A400": 4020990,
        "A700": 3166206
    },
    "Blue": {
        "100": 12312315,
        "200": 9489145,
        "300": 6600182,
        "400": 4367861,
        "500": 2201331,
        "600": 2001125,
        "700": 1668818,
        "800": 1402304,
        "900": 870305,
        "Blue": 2201331,
        "050": 14938877,
        "A100": 8565247,
        "A200": 4492031,
        "A400": 2718207,
        "A700": 2712319
    },
    "LightBlue": {
        "100": 11789820,
        "200": 8508666,
        "300": 5227511,
        "400": 2733814,
        "500": 240116,
        "600": 236517,
        "700": 166097,
        "800": 161725,
        "900": 87963,
        "Light Blue": 240116,
        "050": 14808574,
        "A100": 8444159,
        "A200": 4244735,
        "A400": 45311,
        "A700": 37354
    },
    "Cyan": {
        "100": 11725810,
        "200": 8445674,
        "300": 5099745,
        "400": 2541274,
        "500": 48340,
        "600": 44225,
        "700": 38823,
        "800": 33679,
        "900": 24676,
        "Cyan": 48340,
        "050": 14743546,
        "A100": 8716287,
        "A200": 1638399,
        "A400": 58879,
        "A700": 47316
    },
    "Teal": {
        "100": 11722715,
        "200": 8440772,
        "300": 5093036,
        "400": 2533018,
        "500": 38536,
        "600": 35195,
        "700": 31083,
        "800": 26972,
        "900": 19776,
        "Teal": 38536,
        "050": 14742257,
        "A100": 11010027,
        "A200": 6619098,
        "A400": 1960374,
        "A700": 49061
    },
    "Green": {
        "100": 13166281,
        "200": 10868391,
        "300": 8505220,
        "400": 6732650,
        "500": 5025616,
        "600": 4431943,
        "700": 3706428,
        "800": 3046706,
        "900": 1793568,
        "Green": 5025616,
        "050": 15267305,
        "A100": 12187338,
        "A200": 6942894,
        "A400": 58998,
        "A700": 51283
    },
    "LightGreen": {
        "100": 14478792,
        "200": 12968357,
        "300": 11457921,
        "400": 10275941,
        "500": 9159498,
        "600": 8172354,
        "700": 6856504,
        "800": 5606191,
        "900": 3369246,
        "LightGreen": 9159498,
        "050": 15857897,
        "A100": 13434768,
        "A200": 11730777,
        "A400": 7798531,
        "A700": 6610199
    },
    "Lime": {
        "100": 15791299,
        "200": 15134364,
        "300": 14477173,
        "400": 13951319,
        "500": 13491257,
        "600": 12634675,
        "700": 11514923,
        "800": 10394916,
        "900": 8550167,
        "Lime": 13491257,
        "050": 16382951,
        "A100": 16056193,
        "A200": 15662913,
        "A400": 13041408,
        "A700": 11463168
    },
    "Yellow": {
        "100": 16775620,
        "200": 16774557,
        "300": 16773494,
        "400": 16772696,
        "500": 16771899,
        "600": 16635957,
        "700": 16498733,
        "800": 16361509,
        "900": 16088855,
        "Yellow": 16771899,
        "050": 16776679,
        "A100": 16777101,
        "A200": 16776960,
        "A400": 16771584,
        "A700": 16766464
    },
    "Amber": {
        "100": 16772275,
        "200": 16769154,
        "300": 16766287,
        "400": 16763432,
        "500": 16761095,
        "600": 16757504,
        "700": 16752640,
        "800": 16748288,
        "900": 16740096,
        "Amber": 16761095,
        "050": 16775393,
        "A100": 16770431,
        "A200": 16766784,
        "A400": 16761856,
        "A700": 16755456
    },
    "Orange": {
        "100": 16769202,
        "200": 16764032,
        "300": 16758605,
        "400": 16754470,
        "500": 16750592,
        "600": 16485376,
        "700": 16088064,
        "800": 15690752,
        "900": 15094016,
        "Orange": 16750592,
        "050": 16774112,
        "A100": 16765312,
        "A200": 16755520,
        "A400": 16748800,
        "A700": 16739584
    },
    "DeepOrange": {
        "100": 16764092,
        "200": 16755601,
        "300": 16747109,
        "400": 16740419,
        "500": 16733986,
        "600": 16011550,
        "700": 15092249,
        "800": 14172949,
        "900": 12531212,
        "Deep Orange": 16733986,
        "050": 16509415,
        "A100": 16752256,
        "A200": 16739904,
        "A400": 16727296,
        "A700": 14494720
    },
    "Brown": {
        "100": 14142664,
        "200": 12364452,
        "300": 10586239,
        "400": 9268835,
        "500": 7951688,
        "600": 7162945,
        "700": 6111287,
        "800": 5125166,
        "900": 4073251,
        "Brown": 7951688,
        "050": 15723497
    },
    "Grey": {
        "100": 16119285,
        "200": 15658734,
        "300": 14737632,
        "400": 12434877,
        "500": 10395294,
        "600": 7697781,
        "700": 6381921,
        "800": 4342338,
        "900": 2171169,
        "Grey": 10395294,
        "050": 16448250
    },
    "BlueGrey": {
        "100": 13621468,
        "200": 11583173,
        "300": 9479342,
        "400": 7901340,
        "500": 6323595,
        "600": 5533306,
        "700": 4545124,
        "800": 3622735,
        "900": 2503224,
        "Blue Grey": 6323595,
        "050": 15527921
    }
};

Generator.generateHexagon = function (shape, numberOfSides, size) {
    var numberOfSides = numberOfSides || 6,
        size = size || 20,
        Xcenter = 0,
        Ycenter = 0;

    shape.graphics.moveTo(Xcenter + size * Math.cos(0), Ycenter + size * Math.sin(0));

    for (var i = 1; i <= numberOfSides; i += 1) {
        shape.graphics.lineTo(Xcenter + size * Math.cos(i * 2 * Math.PI / numberOfSides), Ycenter + size * Math.sin(i * 2 * Math.PI / numberOfSides));
    }

    return shape;
};
Generator.generateGrid = function (shape, width, height, p, q) {

    var bw = width || 1280;
    var bh = height || 720;
    var p = 10;
    var q = q || 40;

    for (var x = 0; x <= bw; x += q) {
        shape.graphics.moveTo(0.5 + x + p, p);
        shape.graphics.lineTo(0.5 + x + p, bh + p);
    }

    for (var x = 0; x <= bh; x += q) {
        shape.graphics.moveTo(p, 0.5 + x + p);
        shape.graphics.lineTo(bw + p, 0.5 + x + p);
    }

    return shape;
};
Generator.drawWedge = function (target, x, y, radius, arc, startAngle, yRadius) {

    startAngle = startAngle || 0;
    yRadius = yRadius || 0;

    if (yRadius == 0)
        yRadius = radius;

    target.moveTo(x, y);

    var segAngle,
        theta,
        angle,
        angleMid,
        segs,
        ax,
        ay,
        bx,
        by,
        cx,
        cy;

    if (Math.abs(arc) > 360)
        arc = 360;

    segs = Math.ceil(Math.abs(arc) / 45);
    segAngle = arc / segs;
    theta = -(segAngle / 180) * Math.PI;
    angle = -(startAngle / 180) * Math.PI;
    if (segs > 0) {
        ax = x + Math.cos(startAngle / 180 * Math.PI) * radius;
        ay = y + Math.sin(-startAngle / 180 * Math.PI) * yRadius;
        target.lineTo(ax, ay);
        for (var i = 0; i < segs; ++i) {
            angle += theta;
            angleMid = angle - (theta / 2);
            bx = x + Math.cos(angle) * radius;
            by = y + Math.sin(angle) * yRadius;
            cx = x + Math.cos(angleMid) * (radius / Math.cos(theta / 2));
            cy = y + Math.sin(angleMid) * (yRadius / Math.cos(theta / 2));
            target.curveTo(cx, cy, bx, by);
        }
        target.lineTo(x, y);
    }
};

/*! 0 - 启动
 * 00:00:00 - 00:03:75
 * 用terminal效果做出启动弹幕的效果
 */
Decorator.Comp0 = function () {

    var Comp0_0 = Composition(
        {
            width: 620,
            height: 345,
            startTime: 0,
            duration: 3750,
            layers: [
                Layer({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(2, 0xBEBEBE);
                        shape.graphics.drawRect(0, 0, 620, 22);
                        shape.graphics.drawRect(0, 22, 620, 625);
                        shape.graphics.lineStyle(0);
                        shape.graphics.beginFill(0xFD5F5E);
                        shape.graphics.drawCircle(15, 11, 6);
                        shape.graphics.endFill();
                        shape.graphics.beginFill(0xFEC22A);
                        shape.graphics.drawCircle(30, 11, 6);
                        shape.graphics.endFill();
                        shape.graphics.beginFill(0x23C649);
                        shape.graphics.drawCircle(45, 11, 6);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    properties: {},
                    inPoint: 0,
                    outPoint: 3750
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.consolas"),
                    inPoint: 0,
                    outPoint: 3750,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 11,
                        lineHeight: 24,
                        text: "koukuko - bash - 80x24"
                    },
                    properties: {
                        x: 310,
                        y: 4
                    }
                }),
                // 0-
                DynamicVectorTextLayer({
                    font: Global._get("font.consolas"),
                    inPoint: 0,
                    outPoint: 3750,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 11,
                        lineHeight: 24,
                        text: "bilibili-danmaku-player:~ koukuko$ "
                    },
                    properties: {
                        x: 5,
                        y: 28
                    }
                }),
                // 0-
                DynamicVectorTextLayer({
                    font: Global._get("font.consolas"),
                    inPoint: 0,
                    outPoint: 3750,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 11,
                        lineHeight: 24,
                        text: function (time) {
                            var text = "                                    make last-order.project.biliScript";
                            var offset = time / 1100;
                            offset = offset > 1 ? 1 : offset;
                            return text.substr(1, 36 + Math.ceil(34 * offset));
                        }
                    },
                    properties: {
                        x: 5,
                        y: 28
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.consolas"),
                    inPoint: 1100,
                    outPoint: 3750,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 11,
                        lineHeight: 24,
                        text: function (time) {
                            var text = [
                                "bilibili-danmaku-player:~ koukuko$ make last-order.project.biliScript",
                                "verbose: Setting Player environment...",
                                "verbose: moduleloader hook loaded successfully.",
                                "verbose: Loading app config...",
                                "verbose: userconfig hook loaded successfully.",
                                "verbose: Exposing global variables... (you can disable this by modifying the properties in `config.globals`)",
                                "verbose: Loading user hooks...",
                                "verbose: userhooks hook loaded successfully.",
                                "silly: Configured view engine, `bilibili`",
                                "verbose: Loading runtime custom response definitions...",
                                "silly: Loading hook: controllers",
                                "silly: Loading hook: sockets",
                                "silly: Loading hook: pubsub",
                                "silly: Loading hook: policies",
                                "silly: Loading hook: services",
                                "verbose: logger hook loaded successfully.",
                                "verbose: request hook loaded successfully.",
                                "verbose: Loading the app's models and adapters...",
                                "verbose: Loading app models...",
                                "verbose: Loading app adapters...",
                                "verbose: blueprints hook loaded successfully.",
                                "verbose: responses hook loaded successfully.",
                                "verbose: controllers hook loaded successfully.",
                                "verbose: Loading policy modules from app...",
                                "verbose: Finished loading policy middleware logic.",
                                "verbose: policies hook loaded successfully.",
                                "verbose: services hook loaded successfully.",
                                "verbose: i18n hook loaded successfully.",
                                "verbose: session hook loaded successfully.",
                                "verbose: sockets hook loaded successfully.",
                                "verbose: Setting default Danmaku view engine to bilibili..."
                            ];

                            var offset = time / 3750;
                            offset = offset > 1 ? 1 : offset;

                            text = text.slice(0, Math.ceil(text.length * offset));
                            text = text.join("\n");

                            return text;
                        }
                    },
                    properties: {
                        x: 5,
                        y: 50
                    }
                })
            ]
        }
    );

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 0,
            duration: 3750,
            layers: [
                // 0-背景
                Layer({
                    source: Solid({width: 1280, height: 720, color: 0x171717}),
                    inPoint: 0,
                    outPoint: 3750
                }),
                DynamicSourceLayer({
                    provider: Comp0_0,
                    inPoint: 0,
                    outPoint: 3750,
                    properties: {
                        x: 460,
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 0, value: 250}),
                                    Keyframe({time: 1100, value: 220}),
                                    Keyframe({time: 3750, value: -150})
                                ]
                            }
                        ),
                        z: -200,
                        rotationY: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 0, value: -8}),
                                    Keyframe({time: 1100, value: -8}),
                                    Keyframe({time: 1200, value: 8})
                                ]
                            }
                        )
                    }
                }),
                // 0-背景
                Layer({
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        matr.createGradientBox(1280, 720, 0, 0, 0);
                        shape.graphics.beginGradientFill("linear", [0x222222, 0x00000], [0, 0.7], [0x00, 0xFF], matr, "pad");
                        shape.graphics.drawRect(0, 0, 1280, 720);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    inPoint: 0,
                    outPoint: 3750
                }),
                // 0-背景2
                Layer({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.beginFill(0);
                        shape.graphics.drawRect(0, 0, 1280, 720);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    inPoint: 2700,
                    outPoint: 3750,
                    properties: {
                        alpha: function (time) {
                            var offset = (time - 2700) / 750;
                            offset = offset > 1 ? 1 : offset;
                            return offset;
                        }
                    }
                })
            ]
        }
    );
};

/*! 1 - 报幕
 * 00:03:75 - 00:31:50
 * 电影宣传式报幕
 */
Decorator.Comp1 = function () {

    // 光斑装饰
    // 光斑
    var Comp1_0_0 = Composition({
        layers: [
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(0xffffff, Math.random() * 0.3);
                    shape = Generator.generateHexagon(shape);
                    shape.graphics.endFill();
                    return shape;
                }()
            })
        ]
    });

    var Comp1_0 = Composition(
        {
            width: 1280,
            height: 720,
            startTime: 3750,
            duration: 27750,
            layers: Factory.replicate(Layer, 25, function (i) {

                var inPoint = 3500 + Math.random() * 2000;

                function random(param) {
                    return Math.random() * param.value;
                }

                var xArray = Factory.replicate(random, 25, function (i) {
                    return [{
                        value: 1280
                    }];
                });

                var yArray = Factory.replicate(random, 25, function (i) {
                    return [{
                        value: 300
                    }];
                });

                return [{
                    source: function () {
                        var shape = Shape();
                        shape.graphics.beginFill(0xffffff, Math.random() * 0.3);
                        shape = Generator.generateHexagon(shape);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    inPoint: inPoint,
                    outPoint: 31500,
                    properties: {
                        x: function (time) {
                            return xArray[Math.ceil((time - inPoint) / 1100)];
                        },
                        y: function (time) {
                            return yArray[Math.ceil((time - inPoint) / 1100)];
                        },
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: inPoint, value: 0}),
                                    Keyframe({time: inPoint + 100, value: 1}),
                                    Keyframe({time: inPoint + 1100, value: 0})
                                ],
                                mode: KeyframesBindMode.repeat
                            })
                    }
                }];
            })
        }
    );

    // 报幕
    var Comp1_1 = Composition(
        {
            width: 1280,
            height: 720,
            startTime: 3750,
            duration: 27750,
            layers: [
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 20000,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 24,
                        lineHeight: 24,
                        text: "COMPOSE"
                    },
                    properties: {
                        x: 640,
                        y: 300,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10600, value: 0}),
                                    Keyframe({time: 17750, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 10600, value: 0}),
                                    Keyframe({time: 10700, value: 1}),
                                    Keyframe({time: 17650, value: 1}),
                                    Keyframe({time: 17750, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 20000,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 20,
                        lineHeight: 24,
                        text: "Kz           "
                    },
                    properties: {
                        x: 563,
                        y: 360,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10600, value: 0}),
                                    Keyframe({time: 17750, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 12400, value: 0}),
                                    Keyframe({time: 12500, value: 1}),
                                    Keyframe({time: 17650, value: 1}),
                                    Keyframe({time: 17750, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 20000,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 20,
                        lineHeight: 24,
                        text: "      Livetune",
                        glyphFillColor: 0xBE1C2F
                    },
                    properties: {
                        x: 563,
                        y: 360,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10600, value: 0}),
                                    Keyframe({time: 17750, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 14100, value: 0}),
                                    Keyframe({time: 14200, value: 1}),
                                    Keyframe({time: 17650, value: 1}),
                                    Keyframe({time: 17750, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 24500,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 24,
                        lineHeight: 24,
                        text: "SONG"
                    },
                    properties: {
                        x: 640,
                        y: 300,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 17600, value: 0}),
                                    Keyframe({time: 24500, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 17600, value: 0}),
                                    Keyframe({time: 17700, value: 1}),
                                    Keyframe({time: 24400, value: 1}),
                                    Keyframe({time: 24500, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 24500,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 20,
                        lineHeight: 24,
                        text: "Hatsune     "
                    },
                    properties: {
                        x: 547,
                        y: 360,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 17600, value: 0}),
                                    Keyframe({time: 24500, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 19300, value: 0}),
                                    Keyframe({time: 19400, value: 1}),
                                    Keyframe({time: 24400, value: 1}),
                                    Keyframe({time: 24500, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 24500,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 20,
                        lineHeight: 24,
                        text: "                 Miku",
                        glyphFillColor: 0x56D2C7
                    },
                    properties: {
                        x: 547,
                        y: 360,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 17600, value: 0}),
                                    Keyframe({time: 24500, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 21050, value: 0}),
                                    Keyframe({time: 21150, value: 1}),
                                    Keyframe({time: 24400, value: 1}),
                                    Keyframe({time: 24500, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 30600,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 24,
                        lineHeight: 24,
                        text: "DANMAKU"
                    },
                    properties: {
                        x: 640,
                        y: 300,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 24500, value: 0}),
                                    Keyframe({time: 30600, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 24500, value: 0}),
                                    Keyframe({time: 24600, value: 1}),
                                    Keyframe({time: 30500, value: 1}),
                                    Keyframe({time: 30600, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 30600,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 20,
                        lineHeight: 24,
                        text: "Akino Mizuho        "
                    },
                    properties: {
                        x: 482,
                        y: 360,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 24500, value: 0}),
                                    Keyframe({time: 30600, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 26300, value: 0}),
                                    Keyframe({time: 26400, value: 1}),
                                    Keyframe({time: 30500, value: 1}),
                                    Keyframe({time: 30600, value: 0})
                                ]
                            })
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.segoe.black"),
                    inPoint: 10000,
                    outPoint: 30600,
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 20,
                        lineHeight: 24,
                        text: "                            Koukuko",
                        glyphFillColor: 0xFCFEBA
                    },
                    properties: {
                        x: 482,
                        y: 360,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 24500, value: 0}),
                                    Keyframe({time: 30600, value: -100})
                                ]
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 10000, value: 0}),
                                    Keyframe({time: 28000, value: 0}),
                                    Keyframe({time: 28100, value: 1}),
                                    Keyframe({time: 30500, value: 1}),
                                    Keyframe({time: 30600, value: 0})
                                ]
                            })
                    }
                })
            ]
        }
    );


    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 3750,
            duration: 27750,
            layers: [
                // 1-背景
                Layer({
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        matr.createGradientBox(1280, 720, Math.PI / 2, 0, 0);
                        shape.graphics.beginGradientFill("linear", [0x020111, 0x3a3a52], [1, 1], [0x16, 0xFF], matr, "pad");
                        shape.graphics.drawRect(0, 0, 1280, 720);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    inPoint: 3750,
                    outPoint: 30500,
                    properties: {
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 3750, value: 0.9}),
                                    Keyframe({time: 3800, value: 1}),
                                    Keyframe({time: 3850, value: 0.9})
                                ],
                                mode: KeyframesBindMode.repeat
                            })
                    }
                }),
                // 2-光斑
                DynamicSourceLayer({
                    provider: Comp1_0,
                    inPoint: 3750,
                    outPoint: 31500,
                    properties: {
                        y: 520
                    }
                }),
                // 3-字幕
                DynamicSourceLayer({
                    provider: Comp1_1,
                    inPoint: 3750,
                    outPoint: 31500
                })

            ]
        }
    );

};

/*! 2 - 前序
 * 00:30:50 - 00:59:50
 * 3d cog
 */
Decorator.Comp2 = function () {

    var compWidth = 3000;
    var compHeight = 720;

    // 文字
    var Comp2_0_0_1 = Composition({
        layers: [
            // 1-僕らが
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 72,
                    lineHeight: 72,
                    text: "僕らが",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 0,
                    y: 0
                }
            }),
            // 1-今歩いてる
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 36,
                    lineHeight: 72,
                    text: "今歩いてる",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 300,
                    y: 0
                }
            }),
            // 1-今歩いてる
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 36,
                    lineHeight: 72,
                    text: "知らない道は",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 300,
                    y: 50
                }
            }),
            // 1-我们现在踏上的 这条未知道路
            DynamicVectorTextLayer({
                font: Global._get("font.msyahei"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 18,
                    lineHeight: 72,
                    text: "我们现在踏上的 这条未知道路",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 0,
                    y: 130
                }
            }),
            // 1-line
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xFFFFFF);
                    shape.graphics.lineTo(640, 0);
                    return shape;
                }(),
                properties: {
                    x: 0,
                    y: 120
                }
            })
        ]
    });

    var Comp2_0_0_2 = Composition({
        layers: [
            // 1-僕らが
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "right",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 72,
                    lineHeight: 72,
                    text: "誰かが",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 0,
                    y: 0
                }
            }),
            // 1-今歩いてる
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "right",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 36,
                    lineHeight: 72,
                    text: "作ろうとして",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: -300,
                    y: 0
                }
            }),
            // 1-今歩いてる
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "right",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 36,
                    lineHeight: 72,
                    text: "できたものじゃないから",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: -300,
                    y: 50
                }
            }),
            // 1-我们现在踏上的 这条未知道路
            DynamicVectorTextLayer({
                font: Global._get("font.msyahei"),
                textProperties: {
                    horizontalAlign: "right",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 18,
                    lineHeight: 72,
                    text: "可不是有人想创造 就做得出来的",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 0,
                    y: 130
                }
            }),
            // 1-line
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xFFFFFF);
                    shape.graphics.lineTo(-640, 0);
                    return shape;
                }(),
                properties: {
                    x: 0,
                    y: 120
                }
            })
        ]
    });

    var Comp2_0_0_3 = Composition({
        layers: [
            // 1-僕らが
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 72,
                    lineHeight: 72,
                    text: "もし君の",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 0,
                    y: 0
                }
            }),
            // 1-今歩いてる
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 36,
                    lineHeight: 72,
                    text: "求めるモノが",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 400,
                    y: 0
                }
            }),
            // 1-今歩いてる
            DynamicVectorTextLayer({
                font: Global._get("font.ume"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 36,
                    lineHeight: 72,
                    text: "無かったとしても",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 400,
                    y: 50
                }
            }),
            // 1-我们现在踏上的 这条未知道路
            DynamicVectorTextLayer({
                font: Global._get("font.msyahei"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 18,
                    lineHeight: 72,
                    text: "就算这里没有 你所追求的东西",
                    glyphFillColor: 0xFFFFFF
                },
                properties: {
                    x: 0,
                    y: 130
                }
            }),
            // 1-line
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xFFFFFF);
                    shape.graphics.lineTo(640, 0);
                    return shape;
                }(),
                properties: {
                    x: 0,
                    y: 120
                }
            })
        ]
    });

    // 场景
    var Comp2_0_0_4 = Composition({
        startTime: 30500,
        duration: 29000,
        layers: [
            // 1-s-顶部
            Layer({
                source: Anchor3D({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(1, 0x1A1A1A);
                        shape = Generator.generateGrid(shape, compWidth, compWidth, 10);
                        shape.graphics.lineStyle(1, 0xeeeeee);
                        shape = Generator.generateGrid(shape, compWidth, compWidth, 10, 200);
                        return shape;

                    }()
                }),
                properties: {
                    x: 1280 / 2,
                    y: 720 / 2 - 600,
                    rotationX: -90
                }
            }),
            // 1-s-底部
            Layer({
                source: Anchor3D({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(1, 0x1A1A1A);
                        shape = Generator.generateGrid(shape, compWidth, compWidth, 10);
                        shape.graphics.lineStyle(1, 0xeeeeee);
                        shape = Generator.generateGrid(shape, compWidth, compWidth, 10, 200);
                        return shape;

                    }()
                }),
                properties: {
                    x: 1280 / 2,
                    y: 720 / 2,
                    rotationX: -90
                }
            }),
            // 1-s-左
            Layer({
                source: Anchor3D({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.beginFill(0);
                        shape.graphics.drawRect(0, 0, compHeight, compWidth);
                        shape.graphics.endFill();
                        shape.graphics.lineStyle(1, 0x1A1A1A);
                        shape = Generator.generateGrid(shape, compHeight, compWidth, 10);
                        shape.graphics.lineStyle(1, 0xeeeeee);
                        shape = Generator.generateGrid(shape, compHeight, compWidth, 10, 200);
                        return shape;
                    }()
                }),
                properties: {
                    x: 0,
                    y: 0,
                    rotationX: -90,
                    rotationZ: 90
                }
            }),
            TrackMatte({
                layer: Layer({
                    source: Anchor3D({
                        source: function () {
                            var shape = Shape();
                            shape.graphics.beginFill(0);
                            shape.graphics.drawRect(0, 0, compHeight, compWidth);
                            shape.graphics.endFill();
                            shape.graphics.lineStyle(1, 0x1A1A1A);
                            shape = Generator.generateGrid(shape, compHeight, compWidth, 10);
                            shape.graphics.lineStyle(1, 0xeeeeee);
                            shape = Generator.generateGrid(shape, compHeight, compWidth, 10, 200);
                            return shape;
                        }()
                    }),
                    properties: {
                        x: 0,
                        y: 0,
                        rotationX: -90,
                        rotationZ: 90
                    }
                }),
                mask: Layer({
                    source: Anchor3D({
                        source: function () {
                            var shape = Shape();
                            shape.graphics.beginFill(0);
                            shape.graphics.drawRect(0, 0, compWidth, 2);
                            return shape;
                        }()
                    }),
                    properties: {
                        x: 0,
                        y: 0,
                        rotationX: -90,
                        rotationZ: 90,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 31500, value: -2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 32500, value: 5000, interpolation: Interpolation.easeInOut})
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        )
                    }
                })
            }),
            // 1-s-右
            Layer({
                source: Anchor3D({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.beginFill(0);
                        shape.graphics.drawRect(0, 0, compHeight, compWidth);
                        shape.graphics.endFill();
                        shape.graphics.lineStyle(1, 0x1A1A1A);
                        shape = Generator.generateGrid(shape, compHeight, compWidth, 10);
                        shape.graphics.lineStyle(1, 0xeeeeee);
                        shape = Generator.generateGrid(shape, compHeight, compWidth, 10, 200);
                        return shape;
                    }()
                }),
                properties: {
                    x: 1280,
                    y: 0,
                    rotationX: -90,
                    rotationZ: 90
                }
            })
        ]
    });

    var Comp2_0_0 = Composition({
        startTime: 30500,
        duration: 29000,
        layers: [
            DynamicSourceLayer({
                inPoint: 30500,
                outPoint: 59500,
                provider: Comp2_0_0_4
            }),
            // 僕らが今歩いてる 知らない道は
            DynamicSourceLayer({
                provider: Comp2_0_0_1,
                properties: {
                    x: -50,
                    y: 100,
                    z: -900,
                    blur: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 30500, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 31800, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 37900, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 38200, value: 50, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    filters: Binder.Link(
                        {
                            name: "blur",
                            linkFunc: function (v) {
                                return [$.createBlurFilter(v, v)];
                            }
                        })
                }
            }),
            // 誰かが作ろうとしてできたものじゃないから
            DynamicSourceLayer({
                provider: Comp2_0_0_2,
                properties: {
                    x: 1250,
                    y: 100,
                    z: -400,
                    blur: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 30500, value: 100, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 30800, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 37900, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 38200, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 44600, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 44900, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 51700, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 52000, value: 100, interpolation: Interpolation.back})
                            ]
                        }
                    ),
                    filters: Binder.Link(
                        {
                            name: "blur",
                            linkFunc: function (v) {
                                return [$.createBlurFilter(v, v)];
                            }
                        })
                }
            }),
            // もし君の求めるモノが 無かったとしても
            DynamicSourceLayer({
                provider: Comp2_0_0_3,
                properties: {
                    x: -50,
                    y: 100,
                    z: 100,
                    blur: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 30500, value: 150, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 30800, value: 100, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 37900, value: 100, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 38200, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 44600, value: 50, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 44900, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 51700, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 52000, value: 50, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    filters: Binder.Link(
                        {
                            name: "blur",
                            linkFunc: function (v) {
                                return [$.createBlurFilter(v, v)];
                            }
                        })
                }
            })
        ]
    });

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 30500,
            duration: 29000,
            layers: [
                DynamicSourceLayer({
                    provider: Comp2_0_0,
                    inPoint: 30500,
                    outPoint: 59500,
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 30500, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 30800, value: 500, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 37900, value: 520, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 38200, value: -450, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 44600, value: -420, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 44900, value: 200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 51700, value: 220, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 52000, value: 0, interpolation: Interpolation.back})
                                ]
                            }
                        ),
                        y: 120,
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 30500, value: 1200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 30800, value: 950, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 37900, value: 900, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 38200, value: 200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 44600, value: 100, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 44900, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 51700, value: -120, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 52000, value: -800, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 52300, value: -2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 59500, value: -2100, interpolation: Interpolation.easeOut})
                                ]
                            }
                        ),
                        rotationY: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 30500, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 30800, value: 10, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 37900, value: 12, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 38200, value: -15, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 44600, value: -17, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 44900, value: 15, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 51700, value: 17, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 52000, value: 0, interpolation: Interpolation.back})
                                ]
                            }
                        )

                    }
                })
            ]
        }
    );
};

/*! 3 - HUD
 * 00:51:80 - 01:13:30
 * 3d cog
 */
Decorator.Comp3 = function () {

    var Comp3_0_1 = Composition({
        startTime: 50800,
        duration: 21500,
        layers: Factory.replicate(Layer, 25, function (i) {

            var inPoint = 52058;
            var num = 25;

            return [{
                source: Anchor({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(10, 0xFFFFFF, 1, true, 'normal', 'none');
                        shape.graphics.moveTo(130, 0);
                        shape.graphics.lineTo(150, 0);
                        return shape;
                    }(),
                    x: -150
                }),
                inPoint: inPoint + i * 20,
                outPoint: 72300,
                properties: {
                    x: 0,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 50800, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 58920, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59120, value: 0.6, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59320, value: 3, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    y: 0,
                    rotationZ: 360 * (i / num)
                }
            }];
        })

    });

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 50800,
            duration: 21500,
            layers: [
                DynamicSourceLayer({
                    provider: Comp3_0_1,
                    inPoint: 50800,
                    outPoint: 72300,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 51800, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73300, value: 900, interpolation: Interpolation.easeInOut})
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        )
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.ume"),
                    inPoint: 50800,
                    outPoint: 72300,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 24,
                        lineHeight: 40,
                        text: "次の今日は"
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 50800, value: 640, interpolation: Interpolation.hold}),
                                    Keyframe({
                                        time: 53819,
                                        value: Math.random() * 1280,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({
                                        time: 53930,
                                        value: Math.random() * 1280,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({
                                        time: 54057,
                                        value: Math.random() * 1280,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({time: 54155, value: 640, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54900, value: 570, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 50800, value: 360, interpolation: Interpolation.hold}),
                                    Keyframe({
                                        time: 53819,
                                        value: Math.random() * 720,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({
                                        time: 53930,
                                        value: Math.random() * 720,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({
                                        time: 54057,
                                        value: Math.random() * 720,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({time: 54155, value: 360, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54900, value: 360, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        scale: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 50800, value: 1, interpolation: Interpolation.hold}),
                                    Keyframe({
                                        time: 53819,
                                        value: Math.random() * 10,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({
                                        time: 53930,
                                        value: Math.random() * 10,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({
                                        time: 54057,
                                        value: Math.random() * 10,
                                        interpolation: Interpolation.hold
                                    }),
                                    Keyframe({time: 54155, value: 1, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54900, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        scaleX: Binder.Link(
                            {
                                name: "scale"
                            }),
                        scaleY: Binder.Link(
                            {
                                name: "scale"
                            }),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 51658, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 51958, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    },
                    animators: [
                        Animator(
                            {
                                selector: RangeSelector(
                                    {
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,

                                            // Playing with offset to have the triangle "pass through" the string.
                                            offset: KeyframesBind(
                                                {
                                                    keyframes: [
                                                        Keyframe({time: 57349, value: 1}),
                                                        Keyframe({time: 58000, value: -1})
                                                    ]
                                                })
                                        }
                                    }),
                                bindings: {
                                    x: 1200,
                                    z: -200,
                                    rotationX: 720,
                                    rotationY: -720,
                                    rotationZ: 360
                                }
                            })
                    ]
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    inPoint: 50800,
                    outPoint: 72300,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 12,
                        lineHeight: 12,
                        text: "在下一个今天"
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 50800, value: 640, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54600, value: 640, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54900, value: 600, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        y: 410,
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 51658, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 52058, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    },
                    animators: [
                        Animator(
                            {
                                selector: RangeSelector(
                                    {
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,

                                            // Playing with offset to have the triangle "pass through" the string.
                                            offset: KeyframesBind(
                                                {
                                                    keyframes: [
                                                        Keyframe({time: 57349, value: 1}),
                                                        Keyframe({time: 58000, value: -1})
                                                    ]
                                                })
                                        }
                                    }),
                                bindings: {
                                    x: 1200,
                                    z: -200,
                                    rotationX: 720,
                                    rotationY: -720,
                                    rotationZ: 360
                                }
                            })
                    ]
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.ume"),
                    inPoint: 50800,
                    outPoint: 72300,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 24,
                        lineHeight: 40,
                        text: "君の手で"
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 54600, value: 640, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54900, value: 730, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        y: 360,
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 54600, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54800, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    },
                    animators: [
                        Animator(
                            {
                                selector: RangeSelector(
                                    {
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,

                                            // Playing with offset to have the triangle "pass through" the string.
                                            offset: KeyframesBind(
                                                {
                                                    keyframes: [
                                                        Keyframe({time: 57349, value: 1}),
                                                        Keyframe({time: 58000, value: -1})
                                                    ]
                                                })
                                        }
                                    }),
                                bindings: {
                                    x: 1200,
                                    rotationX: 720,
                                    rotationY: -720,
                                    rotationZ: 360
                                }
                            })
                    ]
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    inPoint: 50800,
                    outPoint: 72300,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 12,
                        lineHeight: 12,
                        text: "用你的手"
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 54600, value: 640, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54900, value: 700, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        y: 410,
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 54600, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 54800, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    },
                    animators: [
                        Animator(
                            {
                                selector: RangeSelector(
                                    {
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,

                                            // Playing with offset to have the triangle "pass through" the string.
                                            offset: KeyframesBind(
                                                {
                                                    keyframes: [
                                                        Keyframe({time: 57349, value: 1}),
                                                        Keyframe({time: 58000, value: -1})
                                                    ]
                                                })
                                        }
                                    }),
                                bindings: {
                                    x: 1200,
                                    z: -200,
                                    rotationX: 720,
                                    rotationY: -720,
                                    rotationZ: 360
                                }
                            })
                    ]
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.ume"),
                    inPoint: 57354,
                    outPoint: 72300,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 24,
                        lineHeight: 40,
                        text: "飾り付けるんだ"
                    },
                    properties: {
                        x: 640,
                        y: 360
                    },
                    animators: [
                        Animator(
                            {
                                selector: RangeSelector(
                                    {
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,

                                            // Playing with offset to have the triangle "pass through" the string.
                                            offset: KeyframesBind(
                                                {
                                                    keyframes: [
                                                        Keyframe({time: 57349, value: -1}),
                                                        Keyframe({time: 58000, value: 1})
                                                    ]
                                                })
                                        }
                                    }),
                                bindings: {
                                    x: -1200,
                                    z: -200,
                                    rotationX: 720,
                                    rotationY: -720,
                                    rotationZ: 360
                                }
                            })
                    ]
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    inPoint: 57354,
                    outPoint: 72300,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 12,
                        lineHeight: 12,
                        text: "装点上就行了"
                    },
                    properties: {
                        x: 640,
                        y: 410
                    },
                    animators: [
                        Animator(
                            {
                                selector: RangeSelector(
                                    {
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,

                                            // Playing with offset to have the triangle "pass through" the string.
                                            offset: KeyframesBind(
                                                {
                                                    keyframes: [
                                                        Keyframe({time: 57349, value: -1}),
                                                        Keyframe({time: 58000, value: 1})
                                                    ]
                                                })
                                        }
                                    }),
                                bindings: {
                                    x: -1200,
                                    z: -200,
                                    rotationX: 720,
                                    rotationY: -720,
                                    rotationZ: 360
                                }
                            })
                    ]
                })
            ]
        }
    );
};

/*! 4 - DECORATOR
 *
 */
Decorator.Comp4 = function () {

    // 1-外围C1
    var Comp4_1 = Composition({
        layers: [
            // 1-外围C1
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xeba68c8);
                    shape.graphics.drawCircle(0, 0, 550);
                    return shape;
                }()
            }),
            // 2-外围C2
            Layer({
                source: function () {
                    var shape = Shape();
                    var num = 200;
                    var r = 460;
                    for (var i = 0; i < num; i++) {
                        shape.graphics.beginFill(0x80deea, Math.random());
                        shape.graphics.drawCircle((Math.sin(2 * Math.PI * (i / num)) * r), (Math.cos(2 * Math.PI * (i / num)) * r), Math.random() * 4 + 2);
                        shape.graphics.endFill();
                    }
                    return shape;
                }()
            })
        ]
    });

    // 1-外围C2
    var Comp4_2 = Composition({
        layers: [
            // 1-外围C1
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xeba68c8);
                    shape.graphics.drawCircle(0, 0, 420);
                    return shape;
                }()
            }),
            // 2-外围C2
            Layer({
                source: function () {
                    var shape = Shape();
                    var num = 200;
                    var r = 360;
                    for (var i = 0; i < num; i++) {
                        shape.graphics.beginFill(0x80deea, Math.random());
                        shape.graphics.drawCircle((Math.sin(2 * Math.PI * (i / num)) * r), (Math.cos(2 * Math.PI * (i / num)) * r), Math.random() * 2 + 2);
                        shape.graphics.endFill();
                    }
                    return shape;
                }()
            })
        ]
    });

    // 3-外围扇形 1/4
    var Comp4_4 = Composition({
        startTime: 59411,
        duration: 14089,
        layers: [
            // 蓝1
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0xec407a, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 240, 360 / 4 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 180, 360 / 4 - 0.02 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0xec407a, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59400, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59900, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60100, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 61100, value: 0.2, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0xec407a, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 240, 360 / 4 - 2, 180 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 180, 360 / 4 + 0.02 - 2, 180 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0xec407a, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59400, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59900, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60100, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 61100, value: 0.2, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            // 蓝1
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 240, 360 / 4 - 2, 360 / 4 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 180, 360 / 4 - 0.02 - 2, 360 / 4 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0x80deea, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59400 + 875, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59900 + 875, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60100 + 875, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 61100 + 875, value: 0.2, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 240, 360 / 4 - 2, 180 + 360 / 4 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 180, 360 / 4 + 0.02 - 2, 180 + 360 / 4 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0x80deea, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59400 + 875, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59900 + 875, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60100 + 875, value: 0.2, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 61100 + 875, value: 0.2, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            })
        ]
    });

    // 2-外围扇形 1/8
    var Comp4_3 = Composition({
        startTime: 59411,
        duration: 14089,
        layers: [
            // 蓝1
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 - 0.02 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0x80deea, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59400, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60400, value: 0.1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60500, value: 1, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 180 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 + 0.02 - 2, 180 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0x80deea, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59400, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60400, value: 0.1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60500, value: 1, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            // 透明1
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 0.3);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 360 / 8 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 - 0.02 - 2, 360 / 8 - 2);
                    shape.graphics.endFill();
                    return shape;
                }()
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 0.3);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 180 + 360 / 8 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 + 0.02 - 2, 180 + 360 / 8 - 2);
                    shape.graphics.endFill();
                    return shape;
                }()
            }),
            // 粉1
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0xec407a, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 360 / 8 * 2 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 - 0.02 - 2, 360 / 8 * 2 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0xec407a, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59900, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60900, value: 0.1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 61000, value: 1, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0xec407a, 1);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 180 + 360 / 8 * 2 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 + 0.02 - 2, 180 + 360 / 8 * 2 - 2);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    filters: [
                        $.createGlowFilter(0xec407a, 1, 64, 64, 1)
                    ],
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59900, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 60900, value: 0.1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 61000, value: 1, interpolation: Interpolation.easeInOut})
                            ],
                            mode: KeyframesBindMode.repeat
                        }
                    )
                }
            }),
            // 透明2
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 0.3);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 360 / 8 * 3 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 - 0.02 - 2, 360 / 8 * 3 - 2);
                    shape.graphics.endFill();
                    return shape;
                }()
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    shape.graphics.beginFill(0x80deea, 0.3);
                    Generator.drawWedge(shape.graphics, 0, 0, 345, 360 / 8 - 2, 180 + 360 / 8 * 3 - 2);
                    Generator.drawWedge(shape.graphics, 0, 0, 280, 360 / 8 + 0.02 - 2, 180 + 360 / 8 * 3 - 2);
                    shape.graphics.endFill();
                    return shape;
                }()
            })
        ]
    });

    // 3-间透明扇形
    var Comp4_5 = Composition({
        layers: Factory.replicate(Layer, 10, function (i) {
            return [
                {
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        shape.graphics.beginFill(0x80deea, 0.2);
                        if (i % 2 == 0) {
                            Generator.drawWedge(shape.graphics, 0, 0, 267, 360 / 10, 36 * i);
                            Generator.drawWedge(shape.graphics, 0, 0, 253, 360 / 10 - 0.02, 36 * i);
                        } else {
                            Generator.drawWedge(shape.graphics, 0, 0, 262, 360 / 10, 36 * i);
                            Generator.drawWedge(shape.graphics, 0, 0, 258, 360 / 10 - 0.02, 36 * i);
                        }

                        shape.graphics.endFill();
                        return shape;
                    }()
                }
            ];
        })
    });

    // 外散线
    var Comp4_6 = Composition({
        layers: [
            Layer({
                source: function () {
                    var shape = Shape();
                    var num = 20;
                    shape.graphics.lineStyle(Math.random() * 3, 0xFFFFFF, 0.5);
                    for (var i = 0; i < num; i++) {
                        var c = 2 * Math.PI * Math.random();
                        var d = Math.random() * 100 + 360;
                        shape.graphics.moveTo(Math.sin(c) * 360, Math.cos(c) * 360);
                        shape.graphics.lineTo(Math.sin(c) * d, Math.cos(c) * d);
                    }
                    return shape;
                }()
            })
        ]
    });

    // 外散线
    var Comp4_7 = Composition({
        layers: [
            Layer({
                source: function () {
                    var shape = Shape();
                    var num = 20;
                    shape.graphics.lineStyle(Math.random() * 3, 0xFFFFFF, 0.5);
                    for (var i = 0; i < num; i++) {
                        var c = 2 * Math.PI * Math.random();
                        var d = Math.random() * 100 + 240;
                        shape.graphics.moveTo(Math.sin(c) * 240, Math.cos(c) * 240);
                        shape.graphics.lineTo(Math.sin(c) * d, Math.cos(c) * d);
                    }
                    return shape;
                }()
            })
        ]
    });

    // 噪音梯形
    var Comp4_8 = Composition({
        startTime: 59411,
        duration: 14089,
        layers: Factory.replicate(Layer, 20, function (i) {

            var inPoint = 59400;

            function random(param) {
                return Math.random() * param.value;
            }

            var xArray = Factory.replicate(random, 200, function (i) {
                return [{
                    value: 1280
                }];
            });

            var yArray = Factory.replicate(random, 200, function (i) {
                return [{
                    value: 720
                }];
            });


            return [
                {
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        var a = Math.random() * 200 + 150;
                        var b = Math.random() * 10 + 10;
                        matr.createGradientBox(400, 720, Math.PI / 2, 0, 0);
                        shape.graphics.beginGradientFill("linear", [0xffffff, 0xffffff], [0.2, 0.8], [0x00, 0xFF], matr, "pad");
                        shape.graphics.lineTo(a, 0);
                        shape.graphics.lineTo(a + Math.random() * 10, b);
                        shape.graphics.lineTo(Math.random() * 10, b);
                        shape.graphics.lineTo(0, 0);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    //inPoint: inPoint + Math.random()* 1000,
                    properties: {
                        x: function (time) {
                            return xArray[Math.ceil((time - inPoint) / 200)];
                        },
                        y: function (time) {
                            return yArray[Math.ceil((time - inPoint) / 200)];
                        },
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: inPoint, value: 0}),
                                    Keyframe({time: inPoint + 100, value: 1}),
                                    Keyframe({time: inPoint + 200, value: 0})
                                ],
                                mode: KeyframesBindMode.repeat
                            })
                    }
                }
            ];
        })
    });

    // LOGO
    var logoText = "DECORATOR".split('');
    var decoratorColorArray = [
        0xf44336,
        0xe91e63,
        0x9c27b0,
        0x673ab7,
        0x3f51b5,
        0x2196f3,
        0x0fb2fc,
        0x00bcd4,
        0x009688
    ];
    var Comp4_9 = Composition({
        startTime: 59411,
        duration: 14089,
        layers: Factory.replicate(DynamicVectorTextLayer, 9, function (i) {

            var offset = Math.random() * 200;

            return [{
                font: Global._get("font.android.ltalic"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 72,
                    lineHeight: 72,
                    text: logoText[i],
                    glyphFillColor: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 59411 + offset, value: 0xffffff}),
                                Keyframe({time: 66364 + offset, value: 0xffffff}),
                                Keyframe({time: 66564 + offset, value: decoratorColorArray[i]}),
                                Keyframe({time: 66764 + offset, value: 0xffffff}),
                                Keyframe({time: 66964 + offset, value: decoratorColorArray[i]})
                            ]
                        })
                },
                properties: {
                    x: i * 80 + 290,
                    y: 360,
                    glowColor: KeyframesBind({
                        keyframes: [
                            Keyframe({time: 59411 + offset, value: 0xffffff}),
                            Keyframe({time: 66364 + offset, value: 0xffffff}),
                            Keyframe({time: 66564 + offset, value: decoratorColorArray[i]}),
                            Keyframe({time: 66764 + offset, value: 0xffffff}),
                            Keyframe({time: 66964 + offset, value: decoratorColorArray[i]})
                        ]
                    }),
                    filters: Binder.Link(
                        {
                            name: "glowColor",
                            linkFunc: function (v) {
                                return [$.createGlowFilter(v, 1, 64, 64, 1)];
                            }
                        })
                }
            }];
        })
    });

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 59411,
            duration: 14089,
            layers: [
                // 0-背景
                Layer({
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        matr.createGradientBox(1280, 720, Math.PI / 2, 0, 0);
                        shape.graphics.beginGradientFill("linear", [0x9c27b0, 0x3f51b5], [1, 1], [0x00, 0xFF], matr, "pad");
                        shape.graphics.drawRect(0, 0, 1280, 720);
                        shape.graphics.endFill();
                        return shape;
                    }()
                }),
                // 1-外围C1
                DynamicSourceLayer({
                    provider: Comp4_1,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 3000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: -100, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 1-外围C2
                DynamicSourceLayer({
                    provider: Comp4_2,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: -3000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: -100, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 3-间透明扇形
                DynamicSourceLayer({
                    provider: Comp4_5,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 3000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 2-外围扇形 1/8
                DynamicSourceLayer({
                    provider: Comp4_3,
                    inPoint: 59411,
                    outPoint: 73500,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 360, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: -100, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 4-外散线 2
                DynamicSourceLayer({
                    provider: Comp4_7,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 6000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 3-外围扇形 1/4
                DynamicSourceLayer({
                    provider: Comp4_4,
                    inPoint: 59411,
                    outPoint: 73500,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: -180, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 100, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 4-外散线 1
                DynamicSourceLayer({
                    provider: Comp4_6,
                    properties: {
                        x: 640,
                        y: 360,
                        rotationZ: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: -6000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 5-内圆
                Layer({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(0.1, 0xffffff);
                        shape.graphics.beginFill(0xfdfdfd, 0.5);
                        shape.graphics.drawCircle(640, 360, 135);
                        shape.graphics.endFill();
                        return shape;
                    }(),
                    properties: {
                        filters: [
                            $.createGlowFilter(0xffffff, 1, 128, 128, 2, 1, true, false),
                            $.createGlowFilter(0xffffff, 1, 128, 128, 2, 1, false, false)
                        ]
                    }
                }),
                // 5-LOGO
                DynamicSourceLayer({
                    provider: Comp4_9,
                    inPoint: 59411,
                    outPoint: 73500,
                    properties: {
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 100, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }

                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.android.ltalic"),
                    inPoint: 50800,
                    outPoint: 73500,
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 16,
                        lineHeight: 16,
                        text: "TeddyLoid_remix_"
                    },
                    properties: {
                        x: 880,
                        y: 430,
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 0}),
                                    Keyframe({time: 66364, value: 0}),
                                    Keyframe({time: 66464, value: 1}),
                                    Keyframe({time: 66564, value: 0}),
                                    Keyframe({time: 66664, value: 1}),
                                    Keyframe({time: 66764, value: 0}),
                                    Keyframe({time: 66864, value: 1}),
                                    Keyframe({time: 72300, value: 1})
                                ]
                            }),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 59411, value: 100, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                // 6 噪音梯形
                DynamicSourceLayer({
                    provider: Comp4_8,
                    inPoint: 59411,
                    outPoint: 73500,
                    properties: {
                        filters: [
                            $.createGlowFilter(0xffffff, 1, 128, 128, 2, 1, true, false),
                            $.createGlowFilter(0xffffff, 1, 128, 128, 2, 1, false, false)
                        ]
                    }
                })
            ]
        });
};

/*! 5 - どんな完璧な計算式できても 君のちょっとで答えは変わるの
 * 73200 - 87000
 */
Decorator.Comp5 = function () {

    var romanNumerals = [
        'I', 'II', 'III', 'IV', 'V', 'VI', 'VII', 'VIII', 'IX', 'X', 'XI', 'XII'
    ].reverse();

    var Copm5_0_0 = Composition({
        layers: Factory.replicate(DynamicVectorTextLayer, 12, function (i) {
            return [
                {
                    font: Global._get("font.arial.black"),
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 40,
                        lineHeight: 40,
                        text: romanNumerals[i]
                    },
                    properties: {
                        x: Math.sin(2 * Math.PI * (i / 12) + Math.PI) * 440,
                        y: Math.cos(2 * Math.PI * (i / 12) + Math.PI) * 440
                    }
                }
            ];
        })
    });

    var Copm5_0_1 = Composition({
        startTime: 73200,
        duration: 13800,
        layers: Factory.replicate(Layer, 50, function (i) {

            var offset = Math.random() * 200;
            var xRandom = Math.random() * 1280;
            var yRandom = Math.random() * 720;
            var xOffset = Math.random() * 360 - 180;
            var yOffset = Math.random() * 180 - 90;

            return [
                {
                    source: Anchor({
                        source: function () {
                            var shape = Shape();
                            shape.graphics.beginFill(0xfdfdfd, 0.5);
                            shape.graphics.drawCircle(0, 0, Math.random() * 5);
                            shape.graphics.endFill();
                            return shape;
                        }()
                    }),
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 73200, value: xRandom, interpolation: Interpolation.easeInOut}),
                                    Keyframe({
                                        time: 87000,
                                        value: xRandom + xOffset,
                                        interpolation: Interpolation.easeInOut
                                    })
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        ),
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 73200, value: yRandom, interpolation: Interpolation.easeInOut}),
                                    Keyframe({
                                        time: 87000,
                                        value: yRandom + yOffset,
                                        interpolation: Interpolation.easeInOut
                                    })
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        ),
                        alpha: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 73200 + offset, value: 1, interpolation: Interpolation.easeInOut}),
                                    Keyframe({
                                        time: 75200 + offset,
                                        value: 0.25,
                                        interpolation: Interpolation.easeInOut
                                    }),
                                    Keyframe({time: 77200 + offset, value: 1, interpolation: Interpolation.easeInOut})
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        )
                    }
                }
            ];
        })
    });

    var Copm5_0 = Composition({
        layers: [
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(5, 0xffffff);
                    shape.graphics.drawCircle(0, 0, 400);
                    return shape;
                }(),
                properties: {
                    z: -20
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(10, 0xffffff);
                    shape.graphics.drawCircle(0, 0, 500);
                    return shape;
                }(),
                properties: {
                    z: -10
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(10, 0xffffff);
                    shape.graphics.drawCircle(0, 0, 600);
                    return shape;
                }()
            }),
            DynamicSourceLayer({
                provider: Copm5_0_0
            })
        ]
    });

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 73200,
            duration: 13800,
            layers: [
                Layer({
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        matr.createGradientBox(1280, 720, Math.PI / 2, 0, 0);
                        shape.graphics.beginGradientFill("linear", [0, 0x000033], [1, 1], [0x16, 0xFF], matr, "pad");
                        shape.graphics.drawRect(0, 0, 1280, 720);
                        shape.graphics.endFill();
                        return shape;
                    }()
                }),
                // 默认星空
                DynamicSourceLayer({
                    provider: Copm5_0_1,
                    inPoint: 73200,
                    outPoint: 87000
                }),
                DynamicSourceLayer({
                    provider: Copm5_0,
                    properties: {
                        x: 640,
                        y: 400,
                        rotationX: -90,
                        rotationY: KeyframesBind(
                            {
                                keyframes: Factory.replicate(Keyframe, 120, function (i) {
                                    var j = Math.floor(i / 2);
                                    var start = 73349;
                                    if (i % 2 == 0) {
                                        return [{
                                            time: start + i * 300,
                                            value: 360 * (j / 12)
                                        }];
                                    } else {
                                        return [{
                                            time: start + i * 300 + 100,
                                            value: 360 * ((j + 1) / 12)
                                        }];
                                    }
                                })
                            }),
                        z: 220,
                        alpha: 0.5
                    }
                }),
                // 背景模糊歌词
                DynamicVectorTextLayer({
                    font: Global._get("font.shingo.heavy"),
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 128,
                        lineHeight: 128,
                        text: "君のちょっとで答えは変わるの",
                        glyphFillColor: 0xFFFFFF
                    },
                    properties: {
                        //x: 3000,
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 73200, value: -2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73746, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80003, value: 2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80503, value: 5000, interpolation: Interpolation.easeInOut})
                                ]
                            }),
                        y: 150,
                        rotationY: 180,
                        filters: [$.createBlurFilter(16, 16)],
                        alpha: 0.9,
                        z: 500
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.shingo.heavy"),
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 128,
                        lineHeight: 128,
                        text: "どんな完璧な計算式できても",
                        glyphFillColor: 0xFFFFFF
                    },
                    properties: {
                        //x: 3000,
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 80503, value: -2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80670, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86643, value: 2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86843, value: 5000, interpolation: Interpolation.easeInOut})
                                ]
                            }),
                        y: 150,
                        rotationY: 180,
                        filters: [$.createBlurFilter(16, 16)],
                        alpha: 0.9,
                        z: 500
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.shingo.heavy"),
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 128,
                        lineHeight: 128,
                        text: "どんな完璧な計算式できても",
                        glyphFillColor: 0xFFFFFF
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 73200, value: 1200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73746, value: 600, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80003, value: -2000, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80503, value: -4000, interpolation: Interpolation.easeInOut})
                                ]
                            }),
                        y: 300,
                        properties: {
                            rotationZ: 90
                        }
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 40,
                        lineHeight: 20,
                        text: "无论做出多么完美的算式",
                        glyphFillColor: 0xFFFFFF
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 73200, value: 1210, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 73746, value: 610, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80003, value: -1900, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80503, value: -4000, interpolation: Interpolation.easeInOut})
                                ]
                            }),
                        y: 470,
                        properties: {
                            rotationZ: 90
                        }
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.shingo.heavy"),
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 128,
                        lineHeight: 128,
                        text: "君のちょっとで答えは変わるの",
                        glyphFillColor: 0xFFFFFF
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 80503, value: 1200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80670, value: 600, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86643, value: -1500, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86843, value: -4000, interpolation: Interpolation.easeInOut})
                                ]
                            }),
                        y: 300,
                        properties: {
                            rotationZ: 90
                        }
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    textProperties: {
                        horizontalAlign: "left",
                        verticalAlign: "top",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 40,
                        lineHeight: 20,
                        text: "结果都会因为你的一点点而改变",
                        glyphFillColor: 0xFFFFFF
                    },
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 80503, value: 1200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 80670, value: 600, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86643, value: -1500, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86843, value: -4000, interpolation: Interpolation.easeInOut})
                                ]
                            }),
                        y: 470,
                        properties: {
                            rotationZ: 90
                        }
                    }
                })
            ]
        });
};
/*! 6 - それだけ誰かの未来は 弱いからきっと ねぇ 明日ができるの
 * 87000 - 101500
 */
Decorator.Comp6 = function () {

    // 流星
    var Comp6_0_0 = Composition({
        startTime: 87000,
        duration: 14500,
        layers: Factory.replicate(Layer, 50, function (i) {

            var x = Math.random() * 1280;
            var blur = [0, 2, 4, 8, 16, 32][Math.ceil(Math.random() * 6)];
            var inPoint = 87000 + Math.random() * 2000;

            function random(param) {
                return Math.random() * param.value;
            }

            var xArray = Factory.replicate(random, 150, function (i) {
                return [{
                    value: 1280
                }];
            });


            return [
                {
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(Math.random() * 10, 0xffffff, Math.random() * 0.8 + 0.2);
                        shape.graphics.lineTo(0, Math.random() * 20 + 20);
                        return shape;
                    }(),
                    inPoint: inPoint,
                    outPoint: 101500,
                    properties: {
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: inPoint, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: inPoint + 100, value: 2000, interpolation: Interpolation.easeInOut})
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        ),
                        x: function (time) {
                            return xArray[Math.ceil((time - inPoint) / 100)];
                        },
                        filters: [$.createBlurFilter(blur, blur)]
                    }
                }
            ];
        })
    });

    function Comp6_1(textJp, textCh) {
        return Composition({
            startTime: 87000,
            duration: 14500,
            layers: [
                DynamicSourceLayer({
                    provider: Composition({
                        layers: [
                            DynamicVectorTextLayer({
                                font: Global._get("font.shingo.heavy"),
                                textProperties: {
                                    horizontalAlign: "left",
                                    verticalAlign: "top",
                                    letterSpacing: 0,
                                    fixedWidth: false,
                                    fontSize: 72,
                                    lineHeight: 72,
                                    text: textJp
                                }
                            }),
                            DynamicVectorTextLayer({
                                font: Global._get("font.msyahei"),
                                textProperties: {
                                    horizontalAlign: "left",
                                    verticalAlign: "top",
                                    letterSpacing: 0,
                                    fixedWidth: false,
                                    fontSize: 24,
                                    lineHeight: 24,
                                    text: textCh
                                },
                                properties: {
                                    y: 120
                                }
                            }),
                            Layer({
                                source: function () {
                                    var shape = Shape();
                                    shape.graphics.lineStyle(10, 0xFFFFFF);
                                    shape.graphics.moveTo(0, 110);
                                    shape.graphics.lineTo(3000, 110);
                                    return shape;
                                }()
                            })
                        ]
                    })
                })
            ]
        });
    }

    var Comp6_2 = Composition({
        startTime: 87000,
        duration: 14500,
        layers: Factory.replicate(Layer, 20, function (i) {

            var blur = [0, 2, 4, 8, 16, 32][Math.ceil(Math.random() * 6)];
            return [
                {
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(5, 0xffffff);
                        shape.graphics.lineTo(0, 2000);
                        return shape;
                    }(),
                    properties: {
                        x: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({
                                        value: Math.random() * 1280 + 2000,
                                        time: 96935,
                                        interpolation: Interpolation.easeInOut
                                    }),
                                    Keyframe({
                                        value: 1280 * ((i + 1) / 21),
                                        time: 97135,
                                        interpolation: Interpolation.easeInOut
                                    }),
                                    Keyframe({
                                        value: 1280 * ((i + 1) / 21),
                                        time: 101500,
                                        interpolation: Interpolation.easeInOut
                                    })
                                ]
                            }
                        ),
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({
                                        value: Math.random() * 720 + 2000,
                                        time: 96935,
                                        interpolation: Interpolation.easeInOut
                                    }),
                                    Keyframe({
                                        value: Math.sin(-Math.PI * ((i + 1) / 21)) * 500 + 850,
                                        time: 97135,
                                        interpolation: Interpolation.easeInOut
                                    }),
                                    Keyframe({
                                        value: Math.sin(-Math.PI * ((i + 1) / 21)) * 500 + 850,
                                        time: 101500,
                                        interpolation: Interpolation.easeInOut
                                    })
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({
                                        value: -Math.random() * 500 - 200,
                                        time: 96935,
                                        interpolation: Interpolation.easeInOut
                                    }),
                                    Keyframe({value: 0, time: 97135, interpolation: Interpolation.easeInOut}),
                                    Keyframe({value: 0, time: 101500, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        blur: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 96935, value: blur, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97135, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 101500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        filters: Binder.Link(
                            {
                                name: "blur",
                                linkFunc: function (v) {
                                    return [$.createBlurFilter(v, v)];
                                }
                            })
                    }
                }
            ];
        })
    });

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 87000,
            duration: 14500,
            layers: [
                Layer({
                    source: function () {
                        var shape = Shape();
                        var matr = $.createMatrix();
                        matr.createGradientBox(1280, 720, Math.PI / 2, 0, 0);
                        shape.graphics.beginGradientFill("linear", [0, 0x000033], [1, 1], [0x16, 0xFF], matr, "pad");
                        shape.graphics.drawRect(0, 0, 1280, 720);
                        shape.graphics.endFill();
                        return shape;
                    }()
                }),
                DynamicSourceLayer({
                    provider: Comp6_0_0,
                    inPoint: 87000,
                    outPoint: 101500,
                    properties: {
                        rotationZ: KeyframesBind({
                                keyframes: [
                                    Keyframe({time: 87000, value: -20, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97000, value: -20, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97200, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 101500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                DynamicSourceLayer({
                    provider: Comp6_1('それだけ', '这就说明'),
                    inPoint: 87000,
                    outPoint: 101500,
                    properties: {
                        rotationZ: 70,
                        x: 500,
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 86750, value: 720, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86894, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 89670, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 89970, value: 720, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93350, value: 2000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 86750, value: -200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 86894, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 89670, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 89970, value: 1000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        blur: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 86750, value: 16, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 88094, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 89670, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 89870, value: 16, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        filters: Binder.Link(
                            {
                                name: "blur",
                                linkFunc: function (v) {
                                    return [$.createBlurFilter(v, v)];
                                }
                            })
                    }
                }),
                DynamicSourceLayer({
                    provider: Comp6_1('誰かの想いは', '人们的想法'),
                    inPoint: 89870,
                    outPoint: 101500,
                    properties: {
                        rotationZ: 70,
                        x: 700,
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 89870, value: 720, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 90094, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93150, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93350, value: 2000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 89870, value: -200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 90094, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93150, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93350, value: 1000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        blur: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 89870, value: 16, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 90094, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93150, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93350, value: 16, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        filters: Binder.Link(
                            {
                                name: "blur",
                                linkFunc: function (v) {
                                    return [$.createBlurFilter(v, v)];
                                }
                            })
                    }
                }),
                DynamicSourceLayer({
                    provider: Comp6_1('強いからきっと　ねぇ', '是强烈的 所以一定 对吧 '),
                    inPoint: 89870,
                    outPoint: 101500,
                    properties: {
                        rotationZ: 70,
                        x: 700,
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 93071, value: 720, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93271, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 96910, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97110, value: 2000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        z: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 93071, value: -200, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93271, value: -50, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 96910, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97110, value: 1000, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        blur: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: 93071, value: 16, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 93271, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 96910, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97110, value: 16, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        ),
                        filters: Binder.Link(
                            {
                                name: "blur",
                                linkFunc: function (v) {
                                    return [$.createBlurFilter(v, v)];
                                }
                            })
                    }
                }),
                DynamicSourceLayer({
                    provider: Comp6_2,
                    inPoint: 87000,
                    outPoint: 101500,
                    properties: {
                        rotationZ: KeyframesBind({
                                keyframes: [
                                    Keyframe({time: 97000, value: -20, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97200, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 101500, value: 0, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.shingo.heavy"),
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 36,
                        lineHeight: 36,
                        text: '明日をつくるの'
                    },
                    properties: {
                        x: 640,
                        y: 260,
                        alpha: KeyframesBind({
                                keyframes: [
                                    Keyframe({time: 97000, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97200, value: 1, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 101500, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 16,
                        lineHeight: 16,
                        text: '可以创造明天'
                    },
                    properties: {
                        x: 640,
                        y: 300,
                        alpha: KeyframesBind({
                                keyframes: [
                                    Keyframe({time: 97000, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 97200, value: 1, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: 101500, value: 1, interpolation: Interpolation.easeInOut})
                                ]
                            }
                        )
                    }
                })
            ]
        }
    );
};

/*! 7 - MG
 * 99536 - 132542
 */
Decorator.Comp7 = function () {

    // 上一个创建闪烁
    function Comp7_0_0(backgroundColor, PointColor, textColor, lineColor) {
        return Composition({
            layers: [
                Layer({
                    source: Solid({width: 1280, height: 720, color: backgroundColor})
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.shingo.heavy"),
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 36,
                        lineHeight: 36,
                        text: '明日をつくるの',
                        glyphFillColor: textColor
                    },
                    properties: {
                        x: 640,
                        y: 260
                    }
                }),
                DynamicVectorTextLayer({
                    font: Global._get("font.msyahei"),
                    textProperties: {
                        horizontalAlign: "center",
                        verticalAlign: "center",
                        letterSpacing: 0,
                        fixedWidth: false,
                        fontSize: 16,
                        lineHeight: 16,
                        text: '可以创造明天',
                        glyphFillColor: textColor
                    },
                    properties: {
                        x: 640,
                        y: 300
                    }
                }),
                DynamicSourceLayer({
                    provider: Composition({
                        layers: Factory.replicate(Layer, 20, function (i) {
                            return [
                                {
                                    source: function () {
                                        var shape = Shape();
                                        shape.graphics.lineStyle(5, lineColor);
                                        shape.graphics.lineTo(0, 2000);
                                        return shape;
                                    }(),
                                    properties: {
                                        x: 1280 * ((i + 1) / 21),
                                        y: Math.sin(-Math.PI * ((i + 1) / 21)) * 500 + 850
                                    }
                                }
                            ];
                        })
                    })
                })
            ]
        });
    }

    var Comp7_0 = Composition({
        startTime: 99536,
        duration: 1201,
        layers: [
            DynamicSourceLayer({
                provider: Comp7_0_0(0x2196f3, 0x2196f3, 0xffffff, 0x90caf9),
                inPoint: 99545,
                properties: {
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 99545, value: 0}),
                                Keyframe({time: 99645, value: 1})
                            ]
                        }
                    )
                }
            }),
            DynamicSourceLayer({
                provider: Comp7_0_0(0x00bcd4, 0x00bcd4, 0xffffff, 0x80deea),
                inPoint: 99745,
                properties: {
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 99745, value: 0}),
                                Keyframe({time: 99851, value: 1})
                            ]
                        }
                    )
                }
            }),
            DynamicSourceLayer({
                provider: Comp7_0_0(0x4caf50, 0x4caf50, 0xffffff, 0xa5d6a7),
                inPoint: 99970,
                properties: {
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 99970, value: 0}),
                                Keyframe({time: 100070, value: 1})
                            ]
                        }
                    )
                }
            })
        ]
    });

    //
    var Comp7_1 = Composition({
        startTime: 100437,
        duration: 1463,
        layers: [
            Layer({
                source: Solid({width: 1280, height: 1280, color: Utils.rgb(65, 147, 194)}),
                inPoint: 100437,
                outPoint: 101900,
                properties: {
                    filters: [
                        $.createGlowFilter(0, 0.5, 4, 4, 1)
                    ],
                    x: 640,
                    y: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 100737, value: 360, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101700, value: 375, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    rotationZ: -135
                }
            }),
            Layer({
                source: Solid({width: 1280, height: 720, color: Utils.rgb(42, 52, 52)}),
                properties: {
                    filters: [
                        $.createGlowFilter(0, 0.5, 4, 4, 1)
                    ],
                    x: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 1280, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 100737, value: 640, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101700, value: 625, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101900, value: 1280, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    y: 360,
                    rotationZ: -45
                }
            }),
            Layer({
                source: Solid({width: 1280, height: 720, color: Utils.rgb(238, 69, 95)}),
                properties: {
                    filters: [
                        $.createGlowFilter(0, 0.5, 4, 4, 1)
                    ],
                    x: 640,
                    y: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 720, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 100737, value: 360, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101700, value: 345, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101900, value: 720, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    rotationZ: 45
                }
            }),
            Layer({
                source: Solid({width: 1280, height: 720, color: Utils.rgb(128, 174, 107)}),
                properties: {
                    filters: [
                        $.createGlowFilter(0, 0.5, 4, 4, 1)
                    ],
                    x: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 100737, value: 640, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101700, value: 655, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    y: 360,
                    rotationZ: 135
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 128,
                    lineHeight: 128,
                    text: "ぼ"
                },
                properties: {
                    x: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 100737, value: 640, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101700, value: 655, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    y: 360,
                    rotationZ: 135
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 128,
                    lineHeight: 128,
                    text: "く"
                },
                properties: {
                    x: 640,
                    y: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 720, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 100737, value: 360, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101700, value: 345, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 720, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    rotationZ: 45
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 128,
                    lineHeight: 128,
                    text: "ら"
                },
                properties: {
                    x: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 1280, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 100737, value: 640, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101700, value: 625, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 1280, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    y: 360,
                    rotationZ: -45
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "left",
                    verticalAlign: "top",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 128,
                    lineHeight: 128,
                    text: "が"
                },
                properties: {
                    x: 640,
                    y: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 100737, value: 360, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101700, value: 375, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    rotationZ: -135
                }
            })

        ]
    });

    var Comp7_2 = Composition({
        startTime: 100437,
        duration: 7563,
        layers: [
            Layer({
                source: Solid({width: 1280, height: 720, color: Utils.rgb(45, 45, 45)})
            }),
            // MG 环绘制 1
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: [
                        // 显示用环
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(0xffffff, 1);
                                Generator.drawWedge(shape.graphics, 0, 0, 330, 180);
                                Generator.drawWedge(shape.graphics, 0, 0, 325, 180);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            properties: {
                                rotationZ: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: 100437 + 1000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 101900 + 1000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102400 + 1000,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102544 + 1000,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        // 遮挡用环
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(Utils.rgb(45, 45, 45), 1);
                                Generator.drawWedge(shape.graphics, 0, 0, 332, 180);
                                Generator.drawWedge(shape.graphics, 0, 0, 223, 180);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            properties: {
                                alpha: 1,
                                rotationZ: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: 100437 + 1000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 101900 + 1000 + 150,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102400 + 1000 + 150,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102544 + 1000 + 150,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360
                }

            }),
            // MG 环绘制 2
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: [
                        // 显示用环
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(0xffffff, 1);
                                Generator.drawWedge(shape.graphics, 0, 0, 200, 180);
                                Generator.drawWedge(shape.graphics, 0, 0, 195, 180);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            properties: {
                                rotationZ: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: 100437 + 1200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 101900 + 1200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102400 + 1200,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102544 + 1200,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        // 遮挡用环
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(Utils.rgb(45, 45, 45), 1);
                                Generator.drawWedge(shape.graphics, 0, 0, 202, 180);
                                Generator.drawWedge(shape.graphics, 0, 0, 192, 180);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            properties: {
                                alpha: 1,
                                rotationZ: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: 100437 + 1200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 101900 + 1200 + 150,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102400 + 1200 + 150,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 102544 + 1200 + 150,
                                                value: 360,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360
                }

            }),
            // 歌词 阴影
            DynamicSourceLayer({
                provider: Composition({
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 36,
                                lineHeight: 36,
                                text: "いまあるいてる",
                                glyphFillColor: 0
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 16,
                                lineHeight: 16,
                                text: "我们现在踏上的",
                                glyphFillColor: 0
                            },
                            properties: {
                                y: 50
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 410,
                    alpha: 0.5,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102044, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108000, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    filters: [$.createBlurFilter(2, 2)]
                }
            }),
            // 歌词 本体
            DynamicSourceLayer({
                provider: Composition({
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 36,
                                lineHeight: 36,
                                text: "いまあるいてる"
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 16,
                                lineHeight: 16,
                                text: "我们现在踏上的"
                            },
                            properties: {
                                y: 50
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102044, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108000, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            // MG 圆扩散
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xffffff);
                    shape.graphics.drawCircle(0, 0, 300);
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102044, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108000, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102044, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102544, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // MG 爆炸线条 5
            DynamicSourceLayer({
                provider: Composition({
                    layers: Factory.replicate(Layer, 5, function (i) {

                        var num = 5;

                        return [{
                            source: Anchor({
                                source: function () {
                                    var shape = Shape();
                                    shape.graphics.lineStyle(2, 0xFFFFFF, 1, true, 'normal', 'none');
                                    shape.graphics.moveTo(130, 0);
                                    shape.graphics.lineTo(200, 0);
                                    return shape;
                                }(),
                                x: -150
                            }),
                            properties: {
                                x: 0,
                                y: 0,
                                rotationZ: 360 * (i / num)
                            }
                        }];
                    })

                }),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437 + 500, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900 + 500, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102044 + 500, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108000 + 500, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437 + 500, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 101900 + 500, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102044 + 500, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 102544 + 500, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // MG 方块 L-R
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: Factory.replicate(Layer, 25, function (i) {

                        var inPoint = 104228 + i * 100;
                        var num = 25;

                        return [{
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(0xffffff);
                                shape.graphics.drawRect(1280 * i / num, 200, 25, 25);
                                shape.graphics.drawRect(1280 * i / num, 520, 25, 25);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            properties: {
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: inPoint,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 100,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 500,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }];
                    })
                })
            }),
            // MG 方块 R-L
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: Factory.replicate(Layer, 25, function (i) {

                        var inPoint = 104228 + 500 + i * 50;
                        var num = 25;

                        return [{
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(0xffffff);
                                shape.graphics.drawRect(1280 * (1 - i / num), 250, 25, 20);
                                shape.graphics.drawRect(1280 * (1 - i / num), 475, 20, 20);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            properties: {
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: inPoint,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 100,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 500,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }];
                    })
                })
            }),
            // MG 矩形变形 1
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(10, 138, 110), 1);
                    shape.graphics.drawRect(-205, -205, 400, 400);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    rotationZ: KeyframesBind({
                            keyframes: [
                                Keyframe({time: 103700, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700 + 500, value: 45, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108000, value: 90, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleY: KeyframesBind({
                            keyframes: [
                                Keyframe({time: 103700, value: 0.001, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700, value: 0.001, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700 + 500, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // MG 矩形变形 2
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(142, 184, 51), 1);
                    shape.graphics.drawRect(-200, -200, 400, 400);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    alpha: 1,
                    rotationZ: KeyframesBind({
                            keyframes: [
                                Keyframe({time: 103700, value: 90, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700, value: 90, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700 + 300, value: 135, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108000, value: 180, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleY: KeyframesBind({
                            keyframes: [
                                Keyframe({time: 103700, value: 0.001, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700, value: 0.001, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700 + 300, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // MG 矩形扩散
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.lineStyle(3, 0xffffff);
                    shape.graphics.drawRect(-250, -250, 500, 500);
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 103700, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103900, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 104100, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    alpha: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 103700, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103700, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 103900, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 104100, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // 方形线条
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: [
                        // T
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(2, 0xFFFFFF, 1, true, 'normal', 'none');
                                shape.graphics.moveTo(640 - 50, 75);
                                shape.graphics.lineTo(640 + 50, 75);
                                return shape;
                            }(),
                            properties: {
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 105000,
                                                value: -200,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105150,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: 400,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 100437,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105150,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        // B
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(2, 0xFFFFFF, 1, true, 'normal', 'none');
                                shape.graphics.moveTo(640 - 50, 720 - 75);
                                shape.graphics.lineTo(640 + 50, 720 - 75);
                                return shape;
                            }(),
                            properties: {
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 105000,
                                                value: 200,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105150,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: -400,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 100437,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105150,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        // L
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(2, 0xFFFFFF, 1, true, 'normal', 'none');
                                shape.graphics.moveTo(280 + 75, 360 - 50);
                                shape.graphics.lineTo(280 + 75, 360 + 50);
                                return shape;
                            }(),
                            properties: {
                                y: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 105000,
                                                value: 200,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105150,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: -400,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 100437,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105150,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        // R
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(2, 0xFFFFFF, 1, true, 'normal', 'none');
                                shape.graphics.moveTo(1280 - 280 - 75, 360 - 50);
                                shape.graphics.lineTo(1280 - 280 - 75, 360 + 50);
                                return shape;
                            }(),
                            properties: {
                                y: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 105000,
                                                value: -200,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105100,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: 400,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 100437,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105000,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105100,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 105200,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        })
                    ]
                })
            }),
            // 歌词2 阴影
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 64,
                                lineHeight: 36,
                                text: "しらないみちは",
                                glyphFillColor: 0
                            },
                            inPoint: 100437,
                            outPoint: 108000,
                            properties: {
                                y: 50,
                                z: KeyframesBind({
                                    keyframes: [
                                        Keyframe({
                                            time: 100437,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({
                                            time: 104228,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({time: 104400, value: 0, interpolation: Interpolation.easeInOutExpo})
                                    ]
                                }),
                                filters: [$.createBlurFilter(2, 2)],
                                alpha: 0.3
                            },
                            animators: [
                                Animator({
                                    selector: RangeSelector({
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,
                                            offset: KeyframesBind({
                                                keyframes: [
                                                    Keyframe({
                                                        time: 100437,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 104228,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 106000,
                                                        value: 1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    })
                                                ]
                                            })
                                        }
                                    }),
                                    bindings: {
                                        alpha: 0
                                    },
                                    blendingFunc: function (value1, value2, effectFactor) {
                                        return 1 - effectFactor;
                                    }
                                })
                            ]
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 16,
                                lineHeight: 16,
                                text: "这条未知道路",
                                glyphFillColor: 0
                            },
                            properties: {
                                y: 120,
                                z: KeyframesBind({
                                    keyframes: [
                                        Keyframe({
                                            time: 100437,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({
                                            time: 104228,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({time: 104400, value: 0, interpolation: Interpolation.easeInOutExpo})
                                    ]
                                }),
                                filters: [$.createBlurFilter(2, 2)],
                                alpha: 0.3
                            },
                            animators: [
                                Animator({
                                    selector: RangeSelector({
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,
                                            offset: KeyframesBind({
                                                keyframes: [
                                                    Keyframe({
                                                        time: 100437,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 104228,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 106000,
                                                        value: 1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    })
                                                ]
                                            })
                                        }
                                    }),
                                    bindings: {
                                        alpha: 0
                                    },
                                    blendingFunc: function (value1, value2, effectFactor) {
                                        return 1 - effectFactor;
                                    }
                                })
                            ]
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360
                }
            }),
            // 歌词2 本体
            DynamicSourceLayer({
                inPoint: 100437,
                outPoint: 108000,
                provider: Composition({
                    startTime: 100437,
                    duration: 7563,
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 64,
                                lineHeight: 36,
                                text: "しらないみちは"
                            },
                            inPoint: 100437,
                            outPoint: 108000,
                            properties: {
                                z: KeyframesBind({
                                    keyframes: [
                                        Keyframe({
                                            time: 100437,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({
                                            time: 104228,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({time: 104400, value: 0, interpolation: Interpolation.easeInOutExpo})
                                    ]
                                })
                            },
                            animators: [
                                Animator({
                                    selector: RangeSelector({
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,
                                            offset: KeyframesBind({
                                                keyframes: [
                                                    Keyframe({
                                                        time: 100437,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 104228,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 106000,
                                                        value: 1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    })
                                                ]
                                            })
                                        }
                                    }),
                                    bindings: {
                                        alpha: 0
                                    },
                                    blendingFunc: function (value1, value2, effectFactor) {
                                        return 1 - effectFactor;
                                    }
                                })
                            ]
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 16,
                                lineHeight: 16,
                                text: "这条未知道路"
                            },
                            properties: {
                                y: 70,
                                z: KeyframesBind({
                                    keyframes: [
                                        Keyframe({
                                            time: 100437,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({
                                            time: 104228,
                                            value: 500,
                                            interpolation: Interpolation.easeInOutExpo
                                        }),
                                        Keyframe({time: 104400, value: 0, interpolation: Interpolation.easeInOutExpo})
                                    ]
                                })
                            },
                            animators: [
                                Animator({
                                    selector: RangeSelector({
                                        shapingFunc: RangeShape.rampUp,
                                        properties: {
                                            start: 0,
                                            end: 1,
                                            offset: KeyframesBind({
                                                keyframes: [
                                                    Keyframe({
                                                        time: 100437,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 104228,
                                                        value: -1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    }),
                                                    Keyframe({
                                                        time: 106000,
                                                        value: 1,
                                                        interpolation: Interpolation.easeInOutExpo
                                                    })
                                                ]
                                            })
                                        }
                                    }),
                                    bindings: {
                                        alpha: 0
                                    },
                                    blendingFunc: function (value1, value2, effectFactor) {
                                        return 1 - effectFactor;
                                    }
                                })
                            ]
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360
                }
            })
        ]
    });

    var Comp7_3_KeyframesBind = KeyframesBind(
        {
            keyframes: [
                Keyframe({time: 107369, value: 0, interpolation: Interpolation.easeInOutExpo}),
                Keyframe({time: 107369 + 100, value: 1, interpolation: Interpolation.easeInOutExpo}),
                Keyframe({time: 108750 - 200, value: 1.5, interpolation: Interpolation.easeInOutExpo}),
                Keyframe({time: 108750, value: 0, interpolation: Interpolation.easeInOutExpo})
            ]
        }
    );

    var Comp7_3 = Composition({
        startTime: 107369,
        duration: 1381,
        layers: [
            Layer({
                source: Solid({width: 350, height: 2000, color: Utils.rgb(244, 207, 101)}),
                properties: {
                    x: 0,
                    y: -500,
                    scaleX: Comp7_3_KeyframesBind
                }
            }),
            Layer({
                source: Solid({width: 280, height: 2000, color: Utils.rgb(139, 171, 140)}),
                properties: {
                    x: 350,
                    y: -500,
                    scaleX: Comp7_3_KeyframesBind
                }
            }),
            Layer({
                source: Solid({width: 350, height: 2000, color: Utils.rgb(211, 120, 105)}),
                properties: {
                    x: 350 + 280,
                    y: -500,
                    scaleX: Comp7_3_KeyframesBind
                }
            }),
            Layer({
                source: Solid({width: 30, height: 2000, color: Utils.rgb(152, 83, 105)}),
                properties: {
                    x: 775,
                    y: -500,
                    scaleX: Comp7_3_KeyframesBind
                }
            }),
            Layer({
                source: Solid({width: 525, height: 2000, color: Utils.rgb(244, 107, 101)}),
                properties: {
                    x: 350 + 280 + 350,
                    y: -500,
                    scaleX: Comp7_3_KeyframesBind
                }
            }),
            Layer({
                source: Solid({width: 525, height: 2000, color: Utils.rgb(235, 173, 96)}),
                properties: {
                    x: 350 + 280 + 350 + 100,
                    y: -500,
                    scaleX: Comp7_3_KeyframesBind
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 107369, value: -100, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 107369 + 100, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108750 - 200, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108750, value: 20, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    fixedWidth: false,
                    fontSize: 120,
                    lineHeight: 120,
                    text: "だれかが"
                },
                properties: {
                    x: 640,
                    y: 360,
                    scaleX: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 107369, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 107369 + 100, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108750 - 200, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 108750, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            })
        ]
    });


    var Comp7_4_0 = Factory.replicate(Layer, 7, function (i) {

        var text = 'つくろうとして'.split('');
        var inPoint = [108723, 108935, 109151, 109335, 109575, 110000, 110466];
        var colors = [
            Utils.rgb(136, 141, 133),
            Utils.rgb(182, 194, 157),
            Utils.rgb(233, 241, 158),
            Utils.rgb(136, 141, 133),
            Utils.rgb(182, 194, 157),
            Utils.rgb(233, 241, 158),
            Utils.rgb(136, 141, 133)
        ];
        var randomArc = Math.random() * 360;
        var x = Math.sin(2 * Math.PI * (randomArc / 360)) * 1280;
        var y = Math.cos(2 * Math.PI * (randomArc / 360)) * 1280;

        return [{
            source: function () {
                var shape = Shape();
                shape.graphics.beginFill(colors[i]);
                shape.graphics.drawCircle(0, 0, 200 - i * 10);
                shape.graphics.endFill();
                return shape;
            }(),
            inPoint: inPoint[i],
            properties: {
                x: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[i], value: x, interpolation: Interpolation.easeOutBack}),
                            Keyframe({time: inPoint[i] + 300, value: 0, interpolation: Interpolation.easeOutBack})
                        ]
                    }
                ),
                y: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[i], value: y, interpolation: Interpolation.easeOutBack}),
                            Keyframe({time: inPoint[i] + 100, value: 0, interpolation: Interpolation.easeOutBack})
                        ]
                    }
                )
            }
        }];
    });
    var Comp7_4_1 = Factory.replicate(DynamicVectorTextLayer, 7, function (i) {

        var text = 'つくろうとして'.split('');
        var inPoint = [108723, 108935, 109151, 109335, 109575, 110000, 110466];
        var colors = [
            Utils.rgb(136, 141, 133),
            Utils.rgb(182, 194, 157),
            Utils.rgb(233, 241, 158),
            Utils.rgb(136, 141, 133),
            Utils.rgb(182, 194, 157),
            Utils.rgb(233, 241, 158),
            Utils.rgb(136, 141, 133)
        ];
        var randomArc = Math.random() * 360;
        var x = Math.sin(2 * Math.PI * (randomArc / 360)) * 1280;
        var y = Math.cos(2 * Math.PI * (randomArc / 360)) * 1280;

        return [{
            font: Global._get("font.ume.p"),
            textProperties: {
                horizontalAlign: "center",
                verticalAlign: "center",
                letterSpacing: 0,
                fixedWidth: false,
                fontSize: 80,
                lineHeight: 80,
                text: text[i]
            },
            inPoint: inPoint[i],
            properties: {
                x: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[i], value: x, interpolation: Interpolation.easeOutBack}),
                            Keyframe({time: inPoint[i] + 300, value: 0, interpolation: Interpolation.easeOutBack})
                        ]
                    }
                ),
                y: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[i], value: y, interpolation: Interpolation.easeOutBack}),
                            Keyframe({time: inPoint[i] + 100, value: 0, interpolation: Interpolation.easeOutBack})
                        ]
                    }
                )
            }
        }];
    });
    var Comp7_4_2 = [];
    for (var i = 0; i < 7; i++) {
        Comp7_4_2.push(Comp7_4_0[i]);
        Comp7_4_2.push(Comp7_4_1[i]);
    }
    var Comp7_4_3 = Factory.replicate(DynamicVectorTextLayer, 8, function (j) {

        var text = ['可', '不', '是', '有', '人', '想', '创', '造'];
        var num = 11;
        var inPoint = 108723 + j * 200;

        return [{
            font: Global._get("font.msyahei"),
            textProperties: {
                horizontalAlign: "center",
                verticalAlign: "center",
                letterSpacing: 0,
                fixedWidth: false,
                fontSize: 36,
                lineHeight: 36,
                text: text[j]
            },
            inPoint: inPoint,
            properties: {
                x: Math.sin(2 * Math.PI * (j / num)) * 250,
                y: Math.cos(2 * Math.PI * (j / num)) * 250,
                alpha: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint, value: 0, interpolation: Interpolation.easeOutBack}),
                            Keyframe({time: inPoint + 200, value: 1, interpolation: Interpolation.easeOutBack}),
                            Keyframe({time: inPoint + 1200, value: 0, interpolation: Interpolation.easeOutBack})
                        ]
                    }
                )
            }
        }];
    });

    var Comp7_4_4 = Factory.replicate(DynamicVectorTextLayer, 11, function (j) {

        var text = 'できたものじゃないから'.split('');
        var num = 11;
        var inPoint = [111118, 111500, 111767, 111958, 112145, 112347, 112629, 112826, 113070, 113500, 113938];

        return [{
            font: Global._get("font.ume.p"),
            textProperties: {
                horizontalAlign: "center",
                verticalAlign: "center",
                letterSpacing: 0,
                fixedWidth: false,
                fontSize: 36,
                lineHeight: 36,
                text: text[j]
            },
            inPoint: inPoint[j],
            properties: {
                x: 100 * j + 145,
                y: 360,
                scale: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[j], value: 0.001, interpolation: Interpolation.easeInOut}),
                            Keyframe({time: inPoint[j] + 200, value: 1, interpolation: Interpolation.easeInOut})
                        ]
                    }
                ),
                scaleX: Binder.Link(
                    {
                        name: "scale"
                    }),
                scaleY: Binder.Link(
                    {
                        name: "scale"
                    })
            }
        }];
    });

    var Comp7_4_6 = Factory.replicate(DynamicVectorTextLayer, 11, function (j) {

        var text = 'できたものじゃないから'.split('');
        var num = 11;
        var inPoint = [111118, 111500, 111767, 111958, 112145, 112347, 112629, 112826, 113070, 113500, 113938];

        return [{
            font: Global._get("font.ume.p"),
            textProperties: {
                horizontalAlign: "center",
                verticalAlign: "center",
                letterSpacing: 0,
                fixedWidth: false,
                fontSize: 36,
                lineHeight: 36,
                text: text[j],
                glyphFillColor: 0
            },
            inPoint: inPoint[j],
            properties: {
                x: 100 * j + 145,
                y: 390,
                alpha: 0.5,
                scale: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[j], value: 0.001, interpolation: Interpolation.easeInOut}),
                            Keyframe({time: inPoint[j] + 200, value: 1, interpolation: Interpolation.easeInOut})
                        ]
                    }
                ),
                scaleX: Binder.Link(
                    {
                        name: "scale"
                    }),
                scaleY: Binder.Link(
                    {
                        name: "scale"
                    }),
                filters: [$.createBlurFilter(2, 2)]
            }
        }];
    });

    var Comp7_4_5 = Factory.replicate(Layer, 11, function (j) {

        var text = 'できたものじゃないから'.split('');
        var num = 11;
        var inPoint = [111118, 111500, 111767, 111958, 112145, 112347, 112629, 112826, 113070, 113500, 113938];

        return [{
            source: function () {
                var shape = Shape();
                shape.graphics.lineStyle(5, 0xffffff);
                shape.graphics.drawRect(-250, -250, 500, 500);
                return shape;
            }(),
            inPoint: inPoint[j],
            properties: {
                x: 100 * j + 145,
                y: 360,
                rotationZ: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[j], value: 0, interpolation: Interpolation.easeInOut}),
                            Keyframe({time: inPoint[j] + 400, value: 180, interpolation: Interpolation.easeInOut}),
                            Keyframe({time: inPoint[j] + 800, value: 360, interpolation: Interpolation.easeInOut})
                        ]
                    }
                ),
                scale: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[j], value: 0.001, interpolation: Interpolation.easeInOut}),
                            Keyframe({time: inPoint[j] + 400, value: 1, interpolation: Interpolation.easeInOut}),
                            Keyframe({time: inPoint[j] + 800, value: 2, interpolation: Interpolation.easeInOut})
                        ]
                    }
                ),
                scaleX: Binder.Link(
                    {
                        name: "scale"
                    }),
                scaleY: Binder.Link(
                    {
                        name: "scale"
                    }),
                alpha: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: inPoint[j], value: 0, interpolation: Interpolation.easeInOutExpo}),
                            Keyframe({time: inPoint[j] + 400, value: 1, interpolation: Interpolation.easeInOutExpo}),
                            Keyframe({time: inPoint[j] + 800, value: 0, interpolation: Interpolation.easeInOutExpo})
                        ]
                    }
                )
            }
        }];

    });


    var Comp7_4 = Composition({
        startTime: 108400,
        duration: 6971,
        layers: [
            Layer({
                source: Solid({width: 1280, height: 720, color: Utils.rgb(140, 163, 68)})
            }),
            DynamicSourceLayer({
                inPoint: 108400,
                outPoint: 115371,
                provider: Composition({
                    startTime: 108400,
                    duration: 6971,
                    layers: Comp7_4_2
                }),
                properties: {
                    x: 640,
                    y: 360
                }
            }),
            DynamicSourceLayer({
                inPoint: 108400,
                outPoint: 115371,
                provider: Composition({
                    startTime: 108400,
                    duration: 6971,
                    layers: Comp7_4_3
                }),
                properties: {
                    x: 640,
                    y: 360
                }
            }),
            // MG 矩形1 切换
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(122, 162, 155));//Utils.rgb(122,162,155)
                    //shape.graphics.drawRect(0,0,1280,720);
                    shape.graphics.drawRect(-1280 / 2, -720 / 2, 1280, 720);
                    shape.graphics.endFill();
                    return shape;
                }(),
                inPoint: 111214,
                properties: {
                    x: 640,
                    y: 360,
                    rotationZ: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 111214, value: 45, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111414, value: 45, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111714, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleY: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 111214, value: 0.001, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111414, value: 0.35, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111414 + 100, value: 0.45, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111614, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 111214, value: 0.001, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111414, value: 0.02, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111614, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // MG 矩形2 切换
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(47, 33, 35));//Utils.rgb(122,162,155)
                    //shape.graphics.drawRect(0,0,1280,720);
                    shape.graphics.drawRect(-1280 / 2, -720 / 2, 1280, 720);
                    shape.graphics.endFill();
                    return shape;
                }(),
                inPoint: 111214 + 200,
                properties: {
                    x: 640,
                    y: 360,
                    rotationZ: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 111214 + 200, value: -45, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111414 + 200, value: -45, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111614 + 500, value: 0, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleY: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({
                                    time: 111214 + 200,
                                    value: 0.001,
                                    interpolation: Interpolation.easeInOutExpo
                                }),
                                Keyframe({time: 111414 + 200, value: 0.35, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({
                                    time: 111414 + 200 + 100,
                                    value: 0.45,
                                    interpolation: Interpolation.easeInOutExpo
                                }),
                                Keyframe({time: 111614 + 200, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({
                                    time: 111214 + 200,
                                    value: 0.001,
                                    interpolation: Interpolation.easeInOutExpo
                                }),
                                Keyframe({time: 111414 + 200, value: 0.02, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 111614 + 200, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    )
                }
            }),
            // MG 矩形逐一
            DynamicSourceLayer({
                inPoint: 111118,
                outPoint: 115371,
                provider: Composition({
                    startTime: 111118,
                    duration: 4253,
                    layers: Comp7_4_5
                })
            }),
            DynamicSourceLayer({
                inPoint: 111118,
                outPoint: 115371,
                provider: Composition({
                    startTime: 111118,
                    duration: 4253,
                    layers: Comp7_4_6
                })
            }),
            // 歌词 正常
            DynamicSourceLayer({
                inPoint: 111118,
                outPoint: 115371,
                provider: Composition({
                    startTime: 111118,
                    duration: 4253,
                    layers: Comp7_4_4
                })
            })
        ]
    });


    var Comp7_5_scale = KeyframesBind(
        {
            keyframes: [
                Keyframe({time: 113918, value: 0.001, interpolation: Interpolation.easeInOut}),
                Keyframe({time: 113918 + 300, value: 1, interpolation: Interpolation.easeInOut}),
                Keyframe({time: 115334, value: 1.2, interpolation: Interpolation.easeInOut}),
                Keyframe({time: 115334 + 200, value: 0.001, interpolation: Interpolation.easeInOut})
            ]
        }
    );
    var Comp7_5 = Composition({
        startTime: 113918,
        duration: 1705,
        layers: [
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(135, 41, 87), 1);
                    shape.graphics.drawCircle(0, 0, 500);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 1100,
                    y: 300,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 250,
                    lineHeight: 250,
                    text: "も"
                },
                properties: {
                    x: 1100,
                    y: 300,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(81, 22, 52), 1);
                    shape.graphics.drawCircle(0, 0, 400);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 350,
                    y: 0,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 200,
                    lineHeight: 200,
                    text: "し"
                },
                properties: {
                    x: 350,
                    y: 0,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(247, 246, 180), 1);
                    shape.graphics.drawCircle(0, 0, 250);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 200,
                    y: 550,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 125,
                    lineHeight: 125,
                    text: "き",
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                },
                properties: {
                    x: 200,
                    y: 550,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(137, 173, 65), 1);
                    shape.graphics.drawCircle(0, 0, 150);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 120,
                    y: 300,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 75,
                    lineHeight: 75,
                    text: "み"
                },
                properties: {
                    x: 120,
                    y: 300,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(135, 41, 87), 1);
                    shape.graphics.drawCircle(0, 0, 200);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 550,
                    y: 500,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 100,
                    lineHeight: 100,
                    text: "の"
                },
                properties: {
                    x: 550,
                    y: 500,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(247, 246, 180), 1);
                    shape.graphics.drawCircle(0, 0, 500);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 600,
                    y: 1000,
                    scale: Comp7_5_scale,
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            })
        ]
    });

    var Comp7_6 = Composition({
        startTime: 114218,
        duration: 4232,
        layers: [
            Layer({
                source: Solid({width: 1280, height: 720, color: Utils.rgb(68, 101, 87)})
            }),
            DynamicSourceLayer({
                inPoint: 114218,
                outPoint: 118450,
                provider: Composition({
                    startTime: 114218,
                    duration: 4232,
                    layers: [
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(Utils.rgb(84, 56, 85), 1);
                                var width = 1000;
                                shape.graphics.drawRect(-width / 2, -width / 2, width, width);
                                shape.graphics.endFill();
                                return shape;
                            }()
                        }),
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(Utils.rgb(86, 110, 124), 1);
                                var width = 750;
                                shape.graphics.drawRect(-width / 2, -width / 2, width, width);
                                shape.graphics.endFill();
                                return shape;
                            }()
                        }),
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(Utils.rgb(237, 173, 143), 1);
                                var width = 400;
                                shape.graphics.drawRect(-width / 2, -width / 2, width, width);
                                shape.graphics.endFill();
                                return shape;
                            }()
                        }),
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(Utils.rgb(202, 111, 106), 1);
                                var width = 100;
                                shape.graphics.drawRect(-width / 2, -width / 2, width, width);
                                shape.graphics.endFill();
                                return shape;
                            }()
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360,
                    alpha: 1,
                    rotationZ: 45
                }
            }),
            // 方形虚线行动
            DynamicSourceLayer({
                inPoint: 114218,
                outPoint: 118450,
                provider: Composition({
                    startTime: 114218,
                    duration: 4232,
                    layers: [
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(3, 0xffffff);
                                var dash = 10;
                                var margin = 10;
                                for (var i = 0; i < 200; i++) {
                                    shape.graphics.moveTo(450, margin * i + dash * i - 1000);
                                    shape.graphics.lineTo(450, margin * i + dash * i + dash - 1000);
                                }
                                return shape;
                            }(),
                            inPoint: 114218,
                            properties: {
                                x: 0,
                                y: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 114218,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 118450,
                                                value: 500,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(3, 0xffffff);
                                var dash = 10;
                                var margin = 10;
                                for (var i = 0; i < 200; i++) {
                                    shape.graphics.moveTo(-450, margin * i + dash * i - 1000);
                                    shape.graphics.lineTo(-450, margin * i + dash * i + dash - 1000);
                                }
                                return shape;
                            }(),
                            inPoint: 114218,
                            properties: {
                                x: 0,
                                y: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 114218,
                                                value: 500,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 118450,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360,
                    alpha: 1,
                    rotationZ: 45
                }
            }),
            // 方形虚线行动
            DynamicSourceLayer({
                inPoint: 114218,
                outPoint: 118450,
                provider: Composition({
                    startTime: 114218,
                    duration: 4232,
                    layers: [
                        // 虚线1 T
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(3, 0xffffff);
                                var dash = 10;
                                var margin = 10;
                                for (var i = 0; i < 200; i++) {
                                    shape.graphics.moveTo(margin * i + dash * i - 1000, 450);
                                    shape.graphics.lineTo(margin * i + dash * i + dash - 1000, 450);
                                }
                                return shape;
                            }(),
                            inPoint: 114218,
                            properties: {
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 114218,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 118450,
                                                value: 500,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        Layer({
                            source: function () {
                                var shape = Shape();
                                shape.graphics.lineStyle(3, 0xffffff);
                                var dash = 10;
                                var margin = 10;
                                for (var i = 0; i < 200; i++) {
                                    shape.graphics.moveTo(margin * i + dash * i - 1000, -450);
                                    shape.graphics.lineTo(margin * i + dash * i + dash - 1000, -450);
                                }
                                return shape;
                            }(),
                            inPoint: 114218,
                            properties: {
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 114218,
                                                value: 500,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 118450,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360,
                    alpha: 1,
                    rotationZ: 45
                }
            }),
            // 歌词
            DynamicVectorTextLayer({
                font: Global._get("font.ume.p"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: KeyframesBind({
                            keyframes: [
                                Keyframe({time: 114218, value: 5, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118450, value: 25, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    fixedWidth: false,
                    fontSize: 48,
                    lineHeight: 48,
                    text: "もとめるモノが"
                },
                properties: {
                    x: 640,
                    y: 360
                }
            }),
            DynamicVectorTextLayer({
                font: Global._get("font.msyahei"),
                textProperties: {
                    horizontalAlign: "center",
                    verticalAlign: "center",
                    letterSpacing: 0,
                    fixedWidth: false,
                    fontSize: 20,
                    lineHeight: 20,
                    text: "就算这里没有"
                },
                properties: {
                    x: 640,
                    y: 420
                }
            })
        ]
    });

    var Comp7_7 = Composition({
        startTime: 117810,
        duration: 3492,
        layers: [
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(252, 201, 110));
                    Generator.generateHexagon(shape, 3, 2000);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118100, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(65, 63, 56));
                    Generator.generateHexagon(shape, 3, 1000);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810 + 200, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118100 + 200, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(216, 63, 20));
                    Generator.generateHexagon(shape, 3, 800);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810 + 400, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118100 + 400, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(253, 222, 165));
                    Generator.generateHexagon(shape, 3, 600);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810 + 600, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118100 + 600, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(178, 197, 171));
                    Generator.generateHexagon(shape, 3, 400);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810 + 800, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118100 + 800, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            Layer({
                source: function () {
                    var shape = Shape();
                    shape.graphics.beginFill(Utils.rgb(113, 117, 99));
                    Generator.generateHexagon(shape, 3, 200);
                    shape.graphics.endFill();
                    return shape;
                }(),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 117810, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810 + 1000, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 118100 + 1000, value: 1, interpolation: Interpolation.easeInOutExpo})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicSourceLayer({
                provider: Composition({
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 36,
                                lineHeight: 36,
                                text: "なかったとしても"
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 16,
                                lineHeight: 16,
                                text: "你所追求的东西"
                            },
                            properties: {
                                y: 50
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 360,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117610, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 121302, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        })
                }
            }),
            DynamicSourceLayer({
                provider: Composition({
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 36,
                                lineHeight: 36,
                                text: "なかったとしても",
                                glyphFillColor: 0
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "center",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 16,
                                lineHeight: 16,
                                text: "你所追求的东西",
                                glyphFillColor: 0
                            },
                            properties: {
                                y: 50
                            }
                        })
                    ]
                }),
                properties: {
                    x: 640,
                    y: 410,
                    alpha: 0.5,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 100437, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117610, value: 0, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 117810, value: 1, interpolation: Interpolation.easeInOutExpo}),
                                Keyframe({time: 121302, value: 1.5, interpolation: Interpolation.easeOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    filters: [$.createBlurFilter(2, 2)]
                }
            })
        ]
    });

    var Comp3_0_1 = Composition({
        startTime: 120438 + 1000,
        duration: 10766,
        layers: Factory.replicate(Layer, 25, function (i) {

            var inPoint = 120438 + 1000;
            var num = 25;

            return [{
                source: Anchor({
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(10, 0xFFFFFF, 1, true, 'normal', 'none');
                        shape.graphics.moveTo(130, 0);
                        shape.graphics.lineTo(150, 0);
                        return shape;
                    }(),
                    x: -150
                }),
                inPoint: inPoint + i * 20,
                outPoint: 72300,
                properties: {
                    x: 0,
                    scale: KeyframesBind(
                        {
                            keyframes: [
                                Keyframe({time: 50800, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 58920, value: 1, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59120, value: 0.6, interpolation: Interpolation.easeInOut}),
                                Keyframe({time: 59320, value: 3, interpolation: Interpolation.easeInOut})
                            ]
                        }
                    ),
                    scaleX: Binder.Link(
                        {
                            name: "scale"
                        }),
                    scaleY: Binder.Link(
                        {
                            name: "scale"
                        }),
                    y: 0,
                    rotationZ: 360 * (i / num)
                }
            }];
        })

    });

    // 流星
    var Comp6_0_0 = Composition({
        startTime: 121438,
        duration: 10766,
        layers: Factory.replicate(Layer, 50, function (i) {

            var x = Math.random() * 1280;
            var blur = [0, 2, 4, 8, 16, 32][Math.ceil(Math.random() * 6)];
            var inPoint = 121438 + Math.random() * 2000;

            function random(param) {
                return Math.random() * param.value;
            }

            var xArray = Factory.replicate(random, 150, function (i) {
                return [{
                    value: 1280
                }];
            });


            return [
                {
                    source: function () {
                        var shape = Shape();
                        shape.graphics.lineStyle(Math.random() * 10, 0xffffff, Math.random() * 0.8 + 0.2);
                        shape.graphics.lineTo(0, Math.random() * 20 + 20);
                        return shape;
                    }(),
                    inPoint: inPoint,
                    properties: {
                        y: KeyframesBind(
                            {
                                keyframes: [
                                    Keyframe({time: inPoint, value: 0, interpolation: Interpolation.easeInOut}),
                                    Keyframe({time: inPoint + 100, value: 2000, interpolation: Interpolation.easeInOut})
                                ],
                                mode: KeyframesBindMode.repeat
                            }
                        ),
                        x: function (time) {
                            return xArray[Math.ceil((time - inPoint) / 100)];
                        },
                        filters: [$.createBlurFilter(blur, blur)]
                    }
                }
            ];
        })
    });

    var Comp7_8 = Composition({
        startTime: 120438,
        duration: 11766,
        layers: [
            //
            Layer({
                source: function () {
                    var shape = Shape();
                    var matr = $.createMatrix();
                    matr.createGradientBox(1280, 720, Math.PI / 2, 0, 0);
                    shape.graphics.beginGradientFill("linear", [0, 0x000033], [1, 1], [0x16, 0xFF], matr, "pad");
                    shape.graphics.drawRect(0, 0, 1280, 720);
                    shape.graphics.endFill();
                    return shape;
                }(),
                inPoint: 120438 + 1000
            }),
            DynamicSourceLayer({
                provider: Comp6_0_0,
                inPoint: 121438,
                outPoint: 132204
            }),
            // mask
            DynamicSourceLayer({
                inPoint: 121438,
                outPoint: 132204,
                provider: Composition({
                    startTime: 120438,
                    duration: 11766,
                    layers: [
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "right",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 36,
                                lineHeight: 36,
                                text: 'つぎのきょうは'
                            },
                            properties: {
                                y: 360,
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 121438,
                                                value: 600,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 123929,
                                                value: 600,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 940,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 123929,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "left",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 32,
                                lineHeight: 32,
                                text: '在下一个今天'
                            },
                            properties: {
                                y: 350,
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 121438,
                                                value: 660,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 123929,
                                                value: 660,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 350,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 123929,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.msyahei"),
                            textProperties: {
                                horizontalAlign: "right",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 36,
                                lineHeight: 36,
                                text: '用你的手'
                            },
                            properties: {
                                y: 350,
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 121438,
                                                value: 940,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 123929,
                                                value: 940,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 600,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 123929,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 126977,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 127177,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        DynamicVectorTextLayer({
                            font: Global._get("font.ume.p"),
                            textProperties: {
                                horizontalAlign: "left",
                                verticalAlign: "center",
                                letterSpacing: 0,
                                fixedWidth: false,
                                fontSize: 32,
                                lineHeight: 32,
                                text: 'きみのてで'
                            },
                            properties: {
                                y: 360,
                                x: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 121438,
                                                value: 350,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 123929,
                                                value: 350,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 660,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                alpha: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 123929,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 124229,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 126977,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 127177,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }),
                        // 歌词 本体
                        DynamicSourceLayer({
                            provider: Composition({
                                layers: [
                                    DynamicVectorTextLayer({
                                        font: Global._get("font.ume.p"),
                                        textProperties: {
                                            horizontalAlign: "center",
                                            verticalAlign: "center",
                                            letterSpacing: 0,
                                            fixedWidth: false,
                                            fontSize: 36,
                                            lineHeight: 36,
                                            text: "かざりつけるんだ"
                                        }
                                    }),
                                    DynamicVectorTextLayer({
                                        font: Global._get("font.msyahei"),
                                        textProperties: {
                                            horizontalAlign: "center",
                                            verticalAlign: "center",
                                            letterSpacing: 0,
                                            fixedWidth: false,
                                            fontSize: 16,
                                            lineHeight: 16,
                                            text: "装点上就行了"
                                        },
                                        properties: {
                                            y: 50
                                        }
                                    })
                                ]
                            }),
                            properties: {
                                x: 640,
                                y: 360,
                                scale: KeyframesBind(
                                    {
                                        keyframes: [
                                            Keyframe({
                                                time: 126677,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 126677,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: 126877,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({time: 132204, value: 1.5, interpolation: Interpolation.easeOut})
                                        ]
                                    }
                                ),
                                scaleX: Binder.Link(
                                    {
                                        name: "scale"
                                    }),
                                scaleY: Binder.Link(
                                    {
                                        name: "scale"
                                    })
                            }
                        })
                    ]
                })
            }),
            // 切换 L
            DynamicSourceLayer({
                inPoint: 120438,
                outPoint: 132204,
                provider: Composition({
                    startTime: 120438,
                    duration: 11766,
                    layers: Factory.replicate(Layer, 5, function (j) {

                        var colors = [
                            Utils.rgb(238, 78, 6),
                            Utils.rgb(75, 161, 170),
                            Utils.rgb(158, 109, 110),
                            Utils.rgb(19, 223, 190),
                            Utils.rgb(116, 130, 149)
                        ];

                        var inPoint = 120438 + j * 300;

                        return [{
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(colors[j], 1);
                                shape.graphics.drawRect(-1280 / 2, 0, 1280 / 2, 720);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            inPoint: inPoint,
                            outPoint: inPoint + 500,
                            properties: {
                                x: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: inPoint,
                                                value: 0,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 200,
                                                value: 1280 / 2,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                scaleX: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: inPoint,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 200,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 500,
                                                value: 0.001,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }];
                    })
                })
            }),
            // 切换 R
            DynamicSourceLayer({
                inPoint: 120438,
                outPoint: 132204,
                provider: Composition({
                    startTime: 120438,
                    duration: 11766,
                    layers: Factory.replicate(Layer, 5, function (j) {

                        var colors = [
                            Utils.rgb(238, 78, 6),
                            Utils.rgb(75, 161, 170),
                            Utils.rgb(158, 109, 110),
                            Utils.rgb(19, 223, 190),
                            Utils.rgb(116, 130, 149)
                        ];

                        var inPoint = 120438 + j * 300;

                        return [{
                            source: function () {
                                var shape = Shape();
                                shape.graphics.beginFill(colors[j], 1);
                                shape.graphics.drawRect(0, 0, 1280 / 2, 720);
                                shape.graphics.endFill();
                                return shape;
                            }(),
                            inPoint: inPoint,
                            outPoint: inPoint + 500,
                            properties: {
                                x: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: inPoint,
                                                value: 1280,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 200,
                                                value: 1280 / 2,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                ),
                                scaleX: KeyframesBind({
                                        keyframes: [
                                            Keyframe({
                                                time: inPoint,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 200,
                                                value: 1,
                                                interpolation: Interpolation.easeInOutExpo
                                            }),
                                            Keyframe({
                                                time: inPoint + 500,
                                                value: 0.001,
                                                interpolation: Interpolation.easeInOutExpo
                                            })
                                        ]
                                    }
                                )
                            }
                        }];
                    })
                })
            })
        ]
    });

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 99536,
            duration: 33006,
            layers: [
                DynamicSourceLayer({
                    provider: Comp7_0,
                    inPoint: 99536,
                    outPoint: 100737
                }),
                // 2 いま あるい てる
                DynamicSourceLayer({
                    provider: Comp7_2,
                    inPoint: 100437,
                    outPoint: 108000
                }),
                // 1 ぼくらが
                DynamicSourceLayer({
                    provider: Comp7_1,
                    inPoint: 100437,
                    outPoint: 101900
                }),
                // 4 だれ か が
                DynamicSourceLayer({
                    provider: Comp7_4,
                    inPoint: 108400,
                    outPoint: 115371
                }),
                // 3 だれ か が
                DynamicSourceLayer({
                    provider: Comp7_3,
                    inPoint: 107369,
                    outPoint: 108750,
                    properties: {
                        x: -150,
                        y: 600,
                        rotationZ: -45
                    }
                }),
                // 6 もとめる モノ が
                DynamicSourceLayer({
                    provider: Comp7_6,
                    inPoint: 114218,
                    outPoint: 118450
                }),
                // 5 もし きみ の
                DynamicSourceLayer({
                    provider: Comp7_5,
                    inPoint: 113918,
                    outPoint: 115623
                }),
                // 7 なかっ た として も
                DynamicSourceLayer({
                    provider: Comp7_7,
                    inPoint: 117810,
                    outPoint: 121302
                }),
                // 8 次の今日は 君の手で
                DynamicSourceLayer({
                    provider: Comp7_8,
                    inPoint: 120438,
                    outPoint: 132204
                })
            ]
        }
    );
};


/*! 8 - DECORATOR
 * 128700 - 144090
 */
Decorator.Comp8 = function () {

    var logo = [];

    logo[0] = [
        /*--图层3--*/
        [[1, 2, 2], [889, 687, 888, 687, 521, -3]],

        /*--图层2--*/
        [[1, 2, 2], [881, 688, 881, 688, 525, -23]],

        /*--图层3--*/
        [[1, 2, 2], [947, 665, 947, 665, 606, -14]],

        /*--图层5--*/
        [[1, 2, 2], [600, -7, 600, -7, 937, 672]],

        /*--图层5--*/
        [[1, 2, 2], [342, 680, 342, 680, 760, -4]],

        /*--图层6--*/
        [[1, 2, 2], [331, 692, 333, 691, 762, -6]],

        /*--图层7--*/
        [[1, 2, 2], [307, 37, 307, 37, 648, 659]],

        /*--图层8--*/
        [[1, 2, 2], [330, 25, 330, 25, 661, 709]],

        /*--图层9--*/
        [[1, 2, 2], [568, 715, 568, 715, 996, 22]],

        /*--图层10--*/
        [[1, 2, 2], [575, 720, 575, 720, 970, 20]],

        /*--图层11--*/
        [[1, 2, 2], [621, 559, 621, 559, 705, 398]],

        /*--图层12--*/
        [[1, 2, 2], [624, 564, 624, 564, 708, 402]],

        /*--图层13--*/
        [[1, 2, 2], [622, 267, 622, 267, 715, 441]],

        /*--图层14--*/
        [[1, 2, 2], [706, 440, 706, 440, 625, 261]],

        /*--图层15--*/
        [[1, 2, 2], [558, 419, 558, 419, 641, 267]],

        /*--图层16--*/
        [[1, 2, 2], [557, 411, 557, 411, 645, 271]],

        /*--图层17--*/
        [[1, 2, 2], [556, 392, 556, 392, 641, 560]],

        /*--图层18--*/
        [[1, 2, 2], [632, 554, 632, 554, 549, 394]],

        /*--图层19--*/
        [[1, 2], [725, 599, 725, 599]],

        /*--图层20--*/
        [[1, 2, 2], [717, 599, 717, 599, 816, 408]],

        /*--图层21--*/
        [[1, 2, 2], [816, 391, 816, 391, 713, 611]],

        /*--图层22--*/
        [[1, 2, 2], [739, 585, 739, 585, 591, 592]],

        /*--图层23--*/
        [[1, 2, 2], [729, 591, 729, 591, 591, 597]],

        /*--图层24--*/
        [[1, 2, 2], [824, 409, 824, 409, 730, 415]],

        /*--图层25--*/
        [[1, 2, 2], [824, 412, 824, 412, 743, 417]],

        /*--图层26--*/
        [[1, 2, 2], [823, 365, 823, 365, 713, 370]],

        /*--图层27--*/
        [[1, 2, 2], [818, 369, 818, 369, 713, 376]],

        /*--图层28--*/
        [[1, 2, 2], [562, 408, 563, 408, 637, 409]],

        /*--图层29--*/
        [[1, 2, 2], [564, 414, 564, 414, 635, 413]],

        /*--图层30--*/
        [[1, 2, 2], [634, 408, 634, 408, 660, 357]],

        /*--图层31--*/
        [[1, 2, 2], [662, 360, 662, 360, 632, 428]],

        /*--图层32--*/
        [[1, 2, 2], [628, 390, 628, 390, 655, 461]],

        /*--图层33--*/
        [[1, 2, 2], [651, 465, 651, 465, 620, 392]]
    ];
    logo[1] = [[[1, 2, 2], [276, 10, 276, 10, 664, 704]],

        /*--图层2--*/
        [[1, 2, 2], [653, 721, 653, 721, 334, -26]],

        /*--图层3--*/
        [[1, 2, 2], [774, -26, 774, -26, 293, 712]],

        /*--图层4--*/
        [[1, 2, 2], [747, -26, 747, -26, 332, 685]],

        /*--图层5--*/
        [[1, 2, 2], [990, -12, 990, -12, 519, 777]],

        /*--图层6--*/
        [[1, 2, 2], [987, -16, 987, -16, 513, 804]],

        /*--图层7--*/
        [[1, 2, 2], [594, 813, 594, 813, 997, 0]],

        /*--图层8--*/
        [[1, 2, 2], [593, 841, 593, 841, 984, 21]],

        /*--图层9--*/
        [[1, 2, 2], [646, 234, 646, 234, 599, 154]],

        /*--图层10--*/
        [[1, 2, 2], [646, 236, 647, 236, 607, 148]],

        /*--图层11--*/
        [[1, 2, 2], [610, 174, 610, 174, 693, 175]],

        /*--图层12--*/
        [[1, 2, 2], [695, 176, 695, 176, 604, 181]],

        /*--图层13--*/
        [[1, 2, 2], [706, 219, 706, 219, 630, 218]],

        /*--图层14--*/
        [[1, 2, 2], [703, 222, 703, 222, 631, 222]],

        /*--图层15--*/
        [[1, 2, 2], [705, 222, 705, 222, 681, 160]],

        /*--图层16--*/
        [[1, 2, 2], [672, 158, 672, 158, 703, 223]],

        /*--图层17--*/
        [[1, 2, 2], [559, 403, 559, 403, 643, 218]],

        /*--图层18--*/
        [[1, 2, 2], [649, 222, 649, 222, 550, 411]],

        /*--图层19--*/
        [[1, 2, 2], [580, 440, 580, 442, 554, 389]],

        /*--图层20--*/
        [[1, 2, 2], [555, 388, 555, 388, 577, 443]],

        /*--图层21--*/
        [[1, 2, 2], [669, 263, 669, 263, 573, 450]],

        /*--图层22--*/
        [[1, 2, 2], [672, 263, 672, 263, 569, 451]],

        /*--图层23--*/
        [[1, 2, 2], [600, 397, 600, 397, 575, 397]],

        /*--图层24--*/
        [[1, 2, 2], [601, 402, 601, 402, 575, 400]],

        /*--图层25--*/
        [[1, 2, 2], [685, 317, 685, 317, 656, 260]],

        /*--图层26--*/
        [[1, 2, 2], [661, 260, 661, 260, 682, 316]],

        /*--图层27--*/
        [[1, 2, 2], [735, 263, 735, 263, 648, 266]],

        /*--图层28--*/
        [[1, 2, 2], [736, 268, 736, 268, 649, 271]],

        /*--图层29--*/
        [[1, 2, 2], [723, 256, 723, 256, 749, 320]],

        /*--图层30--*/
        [[1, 2, 2], [748, 311, 748, 311, 726, 252]],

        /*--图层31--*/
        [[1, 2, 2], [749, 311, 749, 311, 671, 309]],

        /*--图层32--*/
        [[1, 2, 2], [749, 315, 749, 315, 666, 314]],

        /*--图层33--*/
        [[1, 2, 2], [703, 353, 703, 353, 728, 409]],

        /*--图层34--*/
        [[1, 2, 2], [707, 354, 710, 354, 732, 408]],

        /*--图层35--*/
        [[1, 2, 2], [777, 352, 777, 352, 695, 350]],

        /*--图层36--*/
        [[1, 2, 2], [776, 358, 776, 358, 699, 355]],

        /*--图层37--*/
        [[1, 2, 2], [789, 400, 789, 400, 763, 346]],

        /*--图层38--*/
        [[1, 2, 2], [765, 339, 765, 339, 785, 406]],

        /*--图层39--*/
        [[1, 2, 2], [784, 395, 784, 395, 713, 400]],

        /*--图层40--*/
        [[1, 2, 2], [788, 402, 788, 402, 720, 403]],

        /*--图层41--*/
        [[1, 2], [624, 526, 624, 526]],

        /*--图层42--*/
        [[1, 2, 2], [618, 528, 618, 528, 704, 357]],

        /*--图层43--*/
        [[1, 2, 2], [625, 534, 625, 534, 702, 354]],

        /*--图层44--*/
        [[1, 2, 2], [602, 485, 602, 485, 684, 311]],

        /*--图层45--*/
        [[1, 2, 2], [602, 497, 602, 497, 687, 311]],

        /*--图层46--*/
        [[1, 2, 2], [625, 536, 625, 536, 596, 481]],

        /*--图层47--*/
        [[1, 2, 2], [622, 537, 622, 537, 593, 489]],

        /*--图层48--*/
        [[1, 2, 2], [643, 489, 643, 489, 619, 489]],

        /*--图层49--*/
        [[1, 2, 2], [645, 493, 645, 493, 618, 492]],

        /*--图层50--*/
        [[1, 2, 2], [731, 355, 731, 355, 746, 317]],

        /*--图层51--*/
        [[1, 2, 2], [730, 358, 729, 358, 739, 317]],

        /*--图层52--*/
        [[1, 2, 2], [684, 264, 684, 264, 707, 220]],

        /*--图层53--*/
        [[1, 2, 2], [688, 265, 688, 265, 709, 223]],

        /*--图层54--*/
        [[1, 2, 2], [1275, 632, 1275, 632, -24, 625]],

        /*--图层55--*/
        [[1, 2, 2], [1280, 643, 1278, 643, -27, 630]]];
    logo[2] = [/*--图层1--*/
        [[1, 2, 2], [299, 750, 299, 750, 654, -17]],

        /*--图层2--*/
        [[1, 2, 2], [308, 754, 308, 749, 646, -22]],

        /*--图层3--*/
        [[1, 2, 2], [1269, 114, 1269, 114, 63, 105]],

        /*--图层4--*/
        [[1, 2, 2], [-18, 82, -18, 82, 1295, 117]],

        /*--图层5--*/
        [[1, 2, 2], [714, 790, 714, 790, 376, 78]],

        /*--图层6--*/
        [[1, 2, 2], [736, 795, 735, 795, 298, -25]],

        /*--图层7--*/
        [[1, 2, 2], [1333, 498, 1333, 498, -3, 590]],

        /*--图层8--*/
        [[1, 2, 2], [1382, 535, 1382, 535, -19, 570]],

        /*--图层9--*/
        [[1, 2, 2], [553, 751, 553, 751, 960, -27]],

        /*--图层10--*/
        [[1, 2, 2], [567, 753, 567, 753, 939, -28]],

        /*--图层11--*/
        [[1, 2, 2], [691, 159, 691, 159, 650, 87]],

        /*--图层12--*/
        [[1, 2, 2], [696, 157, 696, 157, 653, 83]],

        /*--图层13--*/
        [[1, 2, 2], [597, 332, 597, 332, 689, 146]],

        /*--图层14--*/
        [[1, 2, 2], [695, 143, 695, 143, 600, 336]],

        /*--图层15--*/
        [[1, 2, 2], [559, 322, 560, 322, 484, 323]],

        /*--图层16--*/
        [[1, 2, 2], [484, 329, 486, 329, 555, 329]],

        /*--图层17--*/
        [[1, 2, 2], [669, 82, 669, 82, 548, 338]],

        /*--图层18--*/
        [[1, 2, 2], [663, 562, 663, 562, 548, 310]],

        /*--图层19--*/
        [[1, 2, 2], [667, 555, 667, 555, 550, 308]],

        /*--图层20--*/
        [[1, 2, 2], [673, 92, 673, 92, 551, 344]],

        /*--图层21--*/
        [[1, 2, 2], [663, 454, 663, 454, 592, 315]],

        /*--图层22--*/
        [[1, 2, 2], [660, 464, 661, 464, 599, 318]],

        /*--图层23--*/
        [[1, 2, 2], [752, 272, 752, 272, 665, 462]],

        /*--图层24--*/
        [[1, 2, 2], [740, 275, 742, 275, 661, 466]],

        /*--图层25--*/
        [[1, 2, 2], [754, 279, 754, 279, 673, 281]],

        /*--图层26--*/
        [[1, 2, 2], [752, 284, 752, 284, 672, 285]],

        /*--图层27--*/
        [[1, 2, 2], [636, 378, 636, 378, 682, 273]],

        /*--图层28--*/
        [[1, 2, 2], [648, 365, 648, 365, 688, 277]],

        /*--图层29--*/
        [[1, 2, 2], [771, 329, 771, 329, 733, 268]],

        /*--图层30--*/
        [[1, 2, 2], [733, 258, 733, 258, 773, 326]]];
    logo[3] = [/*--图层1--*/
        [[1, 2, 2], [301, 763, 301, 763, 713, 0]],

        /*--图层2--*/
        [[1, 2, 2], [323, 754, 325, 753, 710, -21]],

        /*--图层3--*/
        [[1, 2, 2], [367, 769, 368, 768, 776, -13]],

        /*--图层4--*/
        [[1, 2, 2, 2, 2], [380, 770, 380, 770, 766, -12, 329, -1, 714, 741]],

        /*--图层5--*/
        [[1, 2, 2], [357, -16, 357, -16, 734, 760]],

        /*--图层6--*/
        [[1, 2, 2], [996, 768, 996, 768, 646, -10]],

        /*--图层7--*/
        [[1, 2, 2], [979, 750, 979, 750, 661, -16]],

        /*--图层8--*/
        [[1, 2, 2], [611, 705, 611, 705, 997, 17]],

        /*--图层9--*/
        [[1, 2, 2], [1019, -12, 1019, -12, 592, 771]],

        /*--图层10--*/
        [[1, 2, 2], [708, 562, 708, 562, 616, 574]],

        /*--图层11--*/
        [[1, 2, 2], [710, 568, 710, 568, 618, 579]],

        /*--图层12--*/
        [[1, 2, 2], [698, 560, 698, 560, 588, 322]],

        /*--图层13--*/
        [[1, 2, 2], [700, 564, 700, 564, 582, 330]],

        /*--图层14--*/
        [[1, 2, 2], [596, 338, 596, 338, 542, 338]],

        /*--图层15--*/
        [[1, 2, 2], [599, 342, 599, 342, 541, 344]],

        /*--图层16--*/
        [[1, 2, 2], [636, 349, 636, 349, 706, 204]],

        /*--图层17--*/
        [[1, 2, 2], [641, 351, 641, 351, 710, 204]],

        /*--图层18--*/
        [[1, 2, 2], [766, 347, 766, 347, 701, 199]],

        /*--图层19--*/
        [[1, 2, 2], [764, 348, 764, 348, 693, 200]],

        /*--图层20--*/
        [[1, 2, 2], [700, 476, 699, 476, 766, 333]],

        /*--图层21--*/
        [[1, 2, 2], [707, 476, 707, 476, 770, 333]],

        /*--图层22--*/
        [[1, 2, 2], [704, 472, 705, 472, 630, 327]],

        /*--图层23--*/
        [[1, 2, 2], [701, 473, 701, 473, 634, 341]],

        /*--图层24--*/
        [[1, 2, 2], [678, 394, 678, 394, 707, 332]],

        /*--图层25--*/
        [[1, 2, 2], [682, 397, 682, 397, 708, 340]],

        /*--图层26--*/
        [[1, 2], [704, 344, 704, 344]],

        /*--图层27--*/
        [[1, 2, 2], [701, 341, 701, 341, 675, 281]],

        /*--图层28--*/
        [[1, 2, 2], [695, 339, 695, 339, 669, 290]],

        /*--图层29--*/
        [[1, 2, 2], [756, 339, 756, 339, 702, 341]],

        /*--图层30--*/
        [[1, 2, 2], [758, 343, 758, 343, 702, 345]]];
    logo[4] = [/*--图层1--*/
        [[1, 2, 2], [986, 775, 986, 775, 551, -17]],

        /*--图层2--*/
        [[1, 2, 2], [545, -12, 545, -12, 971, 767]],

        /*--图层3--*/
        [[1, 2, 2], [991, 747, 992, 747, 632, -12]],

        /*--图层4--*/
        [[1, 2, 2], [992, 752, 992, 752, 625, -13]],

        /*--图层5--*/
        [[1, 2, 2], [336, 755, 336, 755, 716, -12]],

        /*--图层6--*/
        [[1, 2, 2], [705, -22, 705, -22, 327, 748]],

        /*--图层7--*/
        [[1, 2], [800, 346, 800, 346]],

        /*--图层8--*/
        [[1, 2, 2], [800, 350, 800, 350, 733, 348]],

        /*--图层9--*/
        [[1, 2, 2], [800, 353, 801, 352, 735, 353]],

        /*--图层10--*/
        [[1, 2, 2], [693, 345, 693, 345, 630, 214]],

        /*--图层11--*/
        [[1, 2, 2], [689, 346, 689, 346, 628, 222]],

        /*--图层12--*/
        [[1, 2, 2], [630, 223, 627, 223, 607, 263]],

        /*--图层13--*/
        [[1, 2, 2], [606, 276, 606, 276, 637, 221]],

        /*--图层14--*/
        [[1, 2, 2], [669, 400, 669, 400, 601, 254]],

        /*--图层15--*/
        [[1, 2, 2], [672, 399, 672, 399, 603, 245]],

        /*--图层16--*/
        [[1, 2, 2], [671, 397, 671, 397, 696, 343]],

        /*--图层17--*/
        [[1, 2, 2], [673, 401, 673, 401, 700, 345]],

        /*--图层18--*/
        [[1, 2], [650, 283, 650, 283]],

        /*--图层19--*/
        [[1, 2, 2], [648, 285, 648, 285, 617, 290]],

        /*--图层20--*/
        [[1, 2, 2], [655, 289, 655, 289, 629, 294]],

        /*--图层21--*/
        [[1, 2], [611, 355, 611, 355]],

        /*--图层22--*/
        [[1, 2, 2], [604, 349, 606, 349, 580, 308]],

        /*--图层23--*/
        [[1, 2, 2], [609, 348, 609, 348, 582, 305]],

        /*--图层24--*/
        [[1, 2, 2], [543, 400, 543, 400, 587, 312]],

        /*--图层25--*/
        [[1, 2, 2], [549, 390, 549, 390, 593, 315]],

        /*--图层26--*/
        [[1, 2, 2], [552, 402, 552, 402, 512, 323]],

        /*--图层27--*/
        [[1, 2, 2], [549, 404, 549, 404, 505, 326]],

        /*--图层28--*/
        [[1, 2, 2], [599, 394, 599, 394, 539, 398]],

        /*--图层29--*/
        [[], []],

        /*--图层30--*/
        [[1, 2, 2], [539, 403, 540, 402, 596, 400]],

        /*--图层31--*/
        [[1, 2, 2], [594, 537, 594, 537, 598, 333]],

        /*--图层32--*/
        [[1, 2, 2], [602, 538, 602, 538, 602, 337]],

        /*--图层33--*/
        [[1, 2, 2], [599, 529, 602, 531, 640, 584]],

        /*--图层34--*/
        [[1, 2, 2], [636, 583, 636, 583, 591, 528]],

        /*--图层35--*/
        [[1, 2, 2], [640, 363, 640, 363, 634, 581]],

        /*--图层36--*/
        [[1, 2, 2], [642, 367, 642, 367, 642, 581]],

        /*--图层37--*/
        [[1, 2, 2], [630, 577, 630, 577, 706, 574]],

        /*--图层38--*/
        [[1, 2, 2], [629, 581, 629, 581, 701, 577]],

        /*--图层39--*/
        [[1, 2, 2], [701, 586, 701, 586, 701, 478]],

        /*--图层40--*/
        [[1, 2, 2, 2], [706, 591, 706, 591, 705, 486, 745, 483]],

        /*--图层41--*/
        [[1, 2, 2], [747, 493, 747, 493, 684, 488]],

        /*--图层42--*/
        [[1, 2, 2], [669, 481, 669, 481, 638, 430]],

        /*--图层43--*/
        [[1, 2, 2], [673, 482, 673, 482, 637, 424]],

        /*--图层44--*/
        [[1, 2, 2], [666, 488, 666, 488, 733, 354]],

        /*--图层45--*/
        [[1, 2, 2], [675, 488, 675, 488, 743, 346]],

        /*--图层46--*/
        [[1, 2, 2], [804, 345, 802, 345, 741, 490]],

        /*--图层47--*/
        [[1, 2, 2], [803, 343, 803, 343, 731, 493]]];
    logo[5] = [/*--图层1--*/
        [[1, 2, 2], [903, 718, 903, 718, 544, 20]],

        /*--图层2--*/
        [[1, 2, 2], [544, -14, 544, -13, 920, 747]],

        /*--图层3--*/
        [[1, 2, 2], [952, 727, 952, 727, 600, -18]],

        /*--图层4--*/
        [[1, 2, 2], [953, 742, 955, 742, 596, -15]],

        /*--图层5--*/
        [[1, 2, 2], [285, 771, 285, 771, 641, -15]],

        /*--图层6--*/
        [[1, 2, 2], [284, 759, 284, 758, 641, -23]],

        /*--图层7--*/
        [[1, 2, 2], [725, 824, 725, 824, 312, -22]],

        /*--图层8--*/
        [[1, 2, 2], [723, 827, 723, 827, 305, -24]],

        /*--图层9--*/
        [[1, 2, 2], [765, 316, 765, 316, 681, 320]],

        /*--图层10--*/
        [[1, 2, 2], [764, 324, 764, 324, 682, 323]],

        /*--图层11--*/
        [[1, 2, 2], [733, 385, 733, 385, 761, 302]],

        /*--图层12--*/
        [[1, 2, 2], [764, 306, 764, 308, 735, 385]],

        /*--图层13--*/
        [[1, 2, 2], [665, 369, 665, 369, 746, 364]],

        /*--图层14--*/
        [[1, 2, 2], [743, 371, 743, 370, 665, 372]],

        /*--图层15--*/
        [[1, 2, 2], [666, 370, 666, 368, 699, 312]],

        /*--图层16--*/
        [[1, 2, 2], [702, 316, 702, 316, 673, 370]],

        /*--图层17--*/
        [[1, 2, 2], [624, 277, 624, 277, 671, 365]],

        /*--图层18--*/
        [[1, 2, 2], [672, 365, 673, 364, 630, 276]],

        /*--图层19--*/
        [[1, 2, 2], [565, 410, 565, 410, 632, 271]],

        /*--图层20--*/
        [[1, 2, 2], [563, 426, 563, 426, 636, 274]],

        /*--图层21--*/
        [[1, 2, 2], [612, 502, 612, 502, 564, 392]],

        /*--图层22--*/
        [[1, 2, 2], [604, 500, 604, 500, 557, 396]],

        /*--图层23--*/
        [[1, 2, 2], [678, 498, 678, 498, 593, 497]],

        /*--图层24--*/
        [[1, 2, 2], [674, 503, 674, 502, 603, 502]],

        /*--图层25--*/
        [[1, 2, 2], [674, 510, 674, 510, 622, 401]],

        /*--图层26--*/
        [[1, 2, 2], [677, 509, 677, 509, 629, 397]],

        /*--图层27--*/
        [[1, 2, 2], [635, 409, 635, 409, 577, 406]],

        /*--图层28--*/
        [[1, 2, 2], [637, 414, 637, 414, 580, 412]],

        /*--图层29--*/
        [[1, 2, 2], [624, 413, 624, 413, 651, 356]],

        /*--图层30--*/
        [[1, 2, 2], [628, 421, 628, 421, 652, 361]],

        /*--图层31--*/
        [[1, 2, 2], [586, 556, 586, 556, 611, 482]],

        /*--图层32--*/
        [[1, 2, 2], [586, 561, 586, 561, 618, 485]],

        /*--图层33--*/
        [[1, 2, 2], [648, 562, 648, 562, 679, 475]],

        /*--图层34--*/
        [[1, 2, 2], [652, 560, 652, 560, 680, 487]],

        /*--图层35--*/
        [[1, 2, 2], [671, 545, 671, 545, 585, 544]],

        /*--图层36--*/
        [[1, 2, 2], [663, 550, 663, 550, 577, 553]],

        /*--图层37--*/
        [[1, 2, 2], [544, 366, 544, 366, 611, 225]],

        /*--图层38--*/
        [[1, 2, 2], [544, 373, 544, 373, 616, 237]],

        /*--图层39--*/
        [[1, 2, 2], [609, 246, 609, 246, 586, 187]],

        /*--图层40--*/
        [[1, 2, 2], [605, 196, 605, 196, 613, 245]],

        /*--图层41--*/
        [[1, 2, 2], [519, 328, 519, 328, 599, 191]],

        /*--图层42--*/
        [[1, 2, 2], [513, 325, 513, 325, 591, 191]],

        /*--图层43--*/
        [[1, 2, 2], [548, 369, 548, 369, 522, 306]],

        /*--图层44--*/
        [[1, 2, 2], [540, 372, 540, 372, 516, 306]],

        /*--图层45--*/
        [[1, 2, 2], [571, 317, 571, 317, 541, 320]],

        /*--图层46--*/
        [[1, 2, 2], [573, 328, 573, 328, 538, 322]],

        /*--图层47--*/
        [[1, 2, 2], [1272, 113, 1272, 113, -7, 78]],

        /*--图层48--*/
        [[1, 2, 2], [-9, 90, -2, 90, 1276, 102]]];
    logo[6] = [/*--图层1--*/
        [[1, 2, 2], [388, 768, 388, 768, 700, -1]],

        /*--图层2--*/
        [[1, 2, 2], [369, 750, 369, 750, 697, -8]],

        /*--图层3--*/
        [[1, 2, 2], [-12, 171, 2, 171, 1097, 198]],

        /*--图层4--*/
        [[1, 2, 2], [1339, 213, 1339, 213, -10, 181]],

        /*--图层5--*/
        [[1, 2, 2], [396, 803, 396, 803, 777, -14]],

        /*--图层6--*/
        [[1, 2, 2], [396, 789, 396, 789, 769, -13]],

        /*--图层7--*/
        [[1, 2, 2], [1053, 728, 1053, 728, 568, -21]],

        /*--图层8--*/
        [[1, 2, 2], [996, 754, 997, 754, 550, -18]],

        /*--图层9--*/
        [[1, 2, 2], [654, 339, 654, 339, 700, 237]],

        /*--图层10--*/
        [[1, 2, 2], [659, 339, 659, 339, 704, 237]],

        /*--图层11--*/
        [[1, 2, 2], [742, 505, 742, 505, 653, 323]],

        /*--图层12--*/
        [[1, 2, 2], [649, 324, 650, 328, 740, 510]],

        /*--图层13--*/
        [[1, 2, 2], [721, 550, 721, 550, 746, 498]],

        /*--图层14--*/
        [[1, 2, 2], [715, 563, 715, 563, 745, 514]],

        /*--图层15--*/
        [[1, 2, 2], [727, 568, 726, 568, 630, 372]],

        /*--图层16--*/
        [[1, 2, 2], [650, 385, 650, 385, 724, 556]],

        /*--图层17--*/
        [[1, 2, 2], [600, 463, 600, 463, 637, 383]],

        /*--图层18--*/
        [[1, 2, 2], [596, 454, 596, 454, 634, 380]],

        /*--图层19--*/
        [[1, 2, 2], [596, 462, 596, 462, 564, 405]],

        /*--图层20--*/
        [[1, 2, 2], [557, 405, 557, 405, 594, 467]],

        /*--图层21--*/
        [[1, 2, 2], [504, 416, 508, 416, 576, 415]],

        /*--图层22--*/
        [[1, 2, 2], [577, 421, 577, 421, 503, 419]],

        /*--图层23--*/
        [[1, 2, 2], [509, 411, 511, 416, 532, 470]],

        /*--图层24--*/
        [[1, 2, 2], [500, 415, 500, 420, 528, 472]],

        /*--图层25--*/
        [[1, 2], [595, 464, 595, 464]],

        /*--图层26--*/
        [[1, 2, 2], [594, 459, 594, 459, 516, 460]],

        /*--图层27--*/
        [[1, 2, 2], [514, 463, 515, 463, 594, 467]],

        /*--图层28--*/
        [[1, 2, 2], [654, 555, 654, 555, 606, 432]],

        /*--图层29--*/
        [[1, 2, 2], [649, 550, 649, 550, 604, 439]],

        /*--图层30--*/
        [[1, 2, 2], [641, 553, 643, 553, 725, 548]],

        /*--图层31--*/
        [[1, 2, 2], [726, 552, 726, 552, 646, 556]]];
    logo[7] = [/*--图层1--*/
        [[1, 2, 2], [316, 380, 316, 380, 317, 282]],

        /*--图层2--*/
        [[1, 2, 2], [319, 384, 319, 384, 323, 286]],

        /*--图层3--*/
        [[1, 2, 2], [349, 323, 349, 323, 302, 320]],

        /*--图层4--*/
        [[1, 2, 2], [351, 328, 351, 328, 301, 325]],

        /*--图层5--*/
        [[1, 2, 2], [350, 369, 350, 369, 353, 321]],

        /*--图层6--*/
        [[1, 2, 2], [349, 317, 349, 317, 346, 367]],

        /*--图层7--*/
        [[1, 2, 2], [351, 363, 351, 363, 329, 363]],

        /*--图层8--*/
        [[1, 2, 2], [329, 369, 329, 369, 349, 370]],

        /*--图层9--*/
        [[1, 2, 2], [363, 359, 363, 359, 363, 298]],

        /*--图层10--*/
        [[1, 2, 2], [368, 360, 367, 360, 367, 296]],

        /*--图层11--*/
        [[1, 2, 2], [388, 315, 388, 314, 445, 316]],

        /*--图层12--*/
        [[1, 2, 2], [444, 321, 444, 321, 390, 320]],

        /*--图层13--*/
        [[1, 2, 2], [422, 344, 422, 344, 426, 284]],

        /*--图层14--*/
        [[1, 2, 2], [430, 349, 430, 349, 429, 282]],

        /*--图层15--*/
        [[1, 2, 2], [385, 342, 385, 342, 435, 343]],

        /*--图层16--*/
        [[1, 2, 2], [436, 346, 436, 346, 385, 345]],

        /*--图层17--*/
        [[1, 2, 2], [390, 371, 390, 371, 390, 337]],

        /*--图层18--*/
        [[1, 2, 2], [395, 371, 395, 371, 395, 338]],

        /*--图层19--*/
        [[1, 2, 2], [439, 368, 439, 368, 391, 366]],

        /*--图层20--*/
        [[1, 2, 2], [389, 372, 389, 372, 436, 371]],

        /*--图层21--*/
        [[1, 2, 2], [447, 293, 446, 292, 449, 317]],

        /*--图层22--*/
        [[1, 2, 2], [450, 317, 450, 317, 452, 290]],

        /*--图层23--*/
        [[1, 2, 2], [453, 318, 453, 318, 459, 292]],

        /*--图层24--*/
        [[1, 2, 2], [461, 317, 461, 317, 458, 290]],

        /*--图层25--*/
        [[1, 2, 2], [488, 343, 488, 343, 486, 297]],

        /*--图层26--*/
        [[1, 2, 2], [504, 342, 504, 342, 484, 339]],

        /*--图层27--*/
        [[1, 2, 2], [489, 349, 489, 349, 490, 293]],

        /*--图层28--*/
        [[1, 2, 2], [482, 342, 485, 342, 503, 345]],

        /*--图层29--*/
        [[1, 2, 2], [524, 390, 524, 390, 525, 287]],

        /*--图层30--*/
        [[1, 2, 2], [530, 285, 530, 287, 528, 393]],

        /*--图层31--*/
        [[1, 2, 2], [627, 313, 627, 313, 555, 313]],

        /*--图层32--*/
        [[1, 2, 2], [554, 318, 555, 318, 625, 318]],

        /*--图层33--*/
        [[1, 2, 2], [629, 365, 629, 365, 628, 304]],

        /*--图层34--*/
        [[1, 2, 2], [624, 372, 624, 372, 623, 305]],

        /*--图层35--*/
        [[1, 2, 2], [580, 357, 582, 357, 628, 358]],

        /*--图层36--*/
        [[1, 2, 2], [629, 362, 629, 362, 581, 365]],

        /*--图层37--*/
        [[1, 2, 2], [648, 377, 648, 377, 648, 298]],

        /*--图层38--*/
        [[1, 2, 2], [654, 370, 654, 370, 653, 301]],

        /*--图层39--*/
        [[1, 2, 2], [713, 321, 713, 321, 665, 325]],

        /*--图层40--*/
        [[1, 2, 2], [710, 328, 710, 328, 669, 325]],

        /*--图层41--*/
        [[1, 2, 2], [692, 374, 692, 374, 692, 299]],

        /*--图层42--*/
        [[1, 2, 2], [695, 376, 695, 376, 698, 300]],

        /*--图层43--*/
        [[1, 2], [775, 298, 775, 298]],

        /*--图层44--*/
        [[1, 2, 2], [775, 297, 775, 297, 739, 299]],

        /*--图层45--*/
        [[1, 2, 2], [773, 304, 773, 304, 737, 299]],

        /*--图层46--*/
        [[1, 2, 2], [771, 322, 771, 322, 767, 291]],

        /*--图层47--*/
        [[1, 2, 2], [773, 322, 773, 322, 773, 287]],

        /*--图层48--*/
        [[1, 2, 2], [732, 312, 733, 311, 777, 312]],

        /*--图层49--*/
        [[1, 2, 2], [774, 316, 774, 316, 733, 315]],

        /*--图层50--*/
        [[1, 2, 2], [734, 332, 734, 332, 739, 306]],

        /*--图层51--*/
        [[1, 2, 2], [737, 337, 737, 337, 743, 306]],

        /*--图层52--*/
        [[1, 2, 2], [791, 327, 791, 327, 734, 324]],

        /*--图层53--*/
        [[1, 2, 2], [791, 331, 791, 331, 733, 329]],

        /*--图层54--*/
        [[1, 2, 2], [786, 375, 786, 375, 786, 323]],

        /*--图层55--*/
        [[1, 2, 2], [790, 376, 790, 376, 788, 319]],

        /*--图层56--*/
        [[1, 2, 2], [791, 368, 791, 368, 744, 366]],

        /*--图层57--*/
        [[1, 2, 2], [789, 373, 789, 373, 741, 370]],

        /*--图层58--*/
        [[1, 2, 2], [750, 376, 750, 376, 748, 341]],

        /*--图层59--*/
        [[1, 2, 2], [753, 375, 753, 375, 751, 341]],

        /*--图层60--*/
        [[1, 2, 2], [772, 346, 772, 346, 740, 346]],

        /*--图层61--*/
        [[1, 2, 2], [770, 350, 770, 350, 746, 350]],

        /*--图层62--*/
        [[1, 2, 2], [767, 379, 767, 379, 767, 339]],

        /*--图层63--*/
        [[1, 2, 2], [771, 377, 771, 377, 771, 341]],

        /*--图层64--*/
        [[1, 2, 2], [818, 376, 818, 376, 819, 291]],

        /*--图层65--*/
        [[1, 2, 2], [820, 372, 820, 372, 825, 287]],

        /*--图层66--*/
        [[1, 2, 2], [854, 341, 854, 341, 813, 340]],

        /*--图层67--*/
        [[1, 2, 2], [854, 337, 854, 337, 812, 334]],

        /*--图层68--*/
        [[1, 2, 2], [847, 373, 847, 373, 847, 323]],

        /*--图层69--*/
        [[1, 2, 2], [850, 370, 850, 372, 851, 324]],

        /*--图层70--*/
        [[1, 2], [885, 365, 885, 365]],

        /*--图层71--*/
        [[1, 2], [875, 361, 876, 361]],

        /*--图层72--*/
        [[1, 2, 2], [842, 364, 842, 364, 885, 367]],

        /*--图层73--*/
        [[1, 2, 2], [841, 372, 841, 372, 881, 369]],

        /*--图层74--*/
        [[1, 2], [910, 377, 910, 377]],

        /*--图层75--*/
        [[1, 2, 2], [905, 371, 905, 371, 914, 294]],

        /*--图层76--*/
        [[1, 2, 2], [916, 371, 916, 371, 914, 288]],

        /*--图层77--*/
        [[1, 2], [938, 317, 938, 317]],

        /*--图层78--*/
        [[1, 2, 2], [893, 315, 893, 314, 938, 318]],

        /*--图层79--*/
        [[1, 2, 2], [937, 321, 937, 321, 895, 318]],

        /*--图层80--*/
        [[1, 2], [951, 319, 951, 319]],

        /*--图层81--*/
        [[1, 2, 2], [951, 319, 951, 319, 956, 295]],

        /*--图层82--*/
        [[1, 2, 2], [960, 319, 960, 319, 955, 294]],

        /*--图层83--*/
        [[1, 2, 2], [964, 317, 964, 317, 959, 292]],

        /*--图层84--*/
        [[1, 2, 2], [962, 318, 962, 318, 967, 293]],

        /*--图层85--*/
        [[1, 2, 2], [929, 326, 933, 326, 967, 333]],

        /*--图层86--*/
        [[1, 2, 2], [971, 336, 971, 336, 939, 334]],

        /*--图层87--*/
        [[1, 2], [972, 366, 972, 366]],

        /*--图层88--*/
        [[1, 2, 2], [969, 365, 969, 365, 927, 365]],

        /*--图层89--*/
        [[1, 2, 2], [969, 370, 969, 370, 929, 369]]];

    return Composition(
        {
            width: 1280,
            height: 720,
            startTime: 128700,
            duration: 15390,
            layers: [
                // 0-背景
                Layer({
                    source: Solid({width: 1280, height: 720, color: 0})
                }),
                // logo[0]
                DynamicSourceLayer({
                    inPoint: 128700,
                    outPoint: 144090,
                    provider: Composition({
                        startTime: 128700,
                        duration: 15390,
                        layers: Factory.replicate(DynamicSourceLayer, logo.length, function (i) {

                            var inPointRaw = [
                                128100,
                                130070,
                                132040,
                                134010,
                                135100,
                                137070,
                                139040,
                                140100
                            ];
                            var inPoint = inPointRaw[i];

                            if ((i + 1) == inPointRaw.length) {
                                var outPoint = 144090;
                            } else {
                                var outPoint = inPointRaw[i + 1] + 500;
                            }

                            return [{
                                inPoint: inPoint,
                                outPoint: outPoint,
                                provider: Composition({
                                    startTime: inPoint,
                                    duration: outPoint - inPoint,
                                    layers: Factory.replicate(Layer, logo[i].length, function (j) {

                                        var layerInPoint = inPoint + Math.random() * 1200;

                                        var randomColor = Math.floor(Math.random() * 0xffffff);
                                        var randomGlow = Math.pow(2, Math.floor(Math.random() * 5));
                                        return [{
                                            source: function () {
                                                var shape = Shape();
                                                var g = shape.graphics;
                                                g.lineStyle(2, randomColor);
                                                g.drawPath($.toIntVector(logo[i][j][0]), $.toNumberVector(logo[i][j][1]));
                                                return shape;
                                            }(),
                                            inPoint: layerInPoint,
                                            properties: {
                                                filters: [
                                                    $.createGlowFilter(randomColor, 1, randomGlow, randomGlow, 1)
                                                ],
                                                alpha: KeyframesBind(
                                                    {
                                                        keyframes: [
                                                            Keyframe({
                                                                time: layerInPoint,
                                                                value: 0.8,
                                                                interpolation: Interpolation.easeInOutExpo
                                                            }),
                                                            Keyframe({
                                                                time: layerInPoint + 200,
                                                                value: 1,
                                                                interpolation: Interpolation.easeInOutExpo
                                                            }),
                                                            Keyframe({
                                                                time: layerInPoint + 100,
                                                                value: 0.8,
                                                                interpolation: Interpolation.easeInOutExpo
                                                            })
                                                        ],
                                                        mode: KeyframesBindMode.repeat
                                                    }
                                                )
                                            }
                                        }];
                                    })
                                }),
                                properties: {
                                    alpha: KeyframesBind(
                                        {
                                            keyframes: [
                                                Keyframe({
                                                    time: inPoint,
                                                    value: 1,
                                                    interpolation: Interpolation.easeInOutExpo
                                                }),
                                                Keyframe({
                                                    time: outPoint - 200,
                                                    value: 1,
                                                    interpolation: Interpolation.easeInOutExpo
                                                }),
                                                Keyframe({
                                                    time: outPoint,
                                                    value: 0,
                                                    interpolation: Interpolation.easeInOutExpo
                                                })
                                            ]
                                        }
                                    )
                                }
                            }];
                        })
                    })
                })
            ]
        }
    );
};

var mainComp = MainComposition({
    width: 1280,
    height: 720,
    startTime: 0,
    duration: 333166,
    layers: [
        DynamicSourceLayer({
            provider: Decorator.Comp0(),
            inPoint: 0,
            outPoint: 3750
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp1(),
            inPoint: 3750,
            outPoint: 31500
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp2(),
            inPoint: 30500,
            outPoint: 59500
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp3(),
            inPoint: 50800,
            outPoint: 72300,
            properties: {
                z: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: 50800, value: 900}),
                            Keyframe({time: 51000, value: 0}),
                            Keyframe({time: 72300, value: -100})
                        ]
                    })
            }
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp4(),
            inPoint: 59411,
            outPoint: 73500,
            properties: {
                alpha: KeyframesBind(
                    {
                        keyframes: [
                            Keyframe({time: 59411, value: 0}),
                            Keyframe({time: 59411 + 100, value: 1})
                        ]
                    })
            }
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp5(),
            inPoint: 73200,
            outPoint: 87000
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp6(),
            inPoint: 87000,
            outPoint: 101500,
            properties: {
                x: KeyframesBind(
                    {
                        keyframes: Factory.replicate(Keyframe, 150, function (i) {
                            return [{
                                time: 87000 + i * 100,
                                value: Math.random() * 20 - 10
                            }];
                        })
                    }),
                y: KeyframesBind(
                    {
                        keyframes: Factory.replicate(Keyframe, 150, function (i) {
                            return [{
                                time: 87000 + i * 100,
                                value: Math.random() * 20 - 10
                            }];
                        })
                    })
            }
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp7(),
            inPoint: 99536,
            outPoint: 132542
        }),
        DynamicSourceLayer({
            provider: Decorator.Comp8(),
            inPoint: 128700,
            outPoint: 144090
        })
    ]
});

Akari.execute(mainComp);