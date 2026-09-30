// M8 缓动函数与 TweenEasing。

// ---- 补间缓动（对齐 M8 移植层 M8Easing 语义，支持 "M8Easing.SineEaseInOut" 全名）----

function linearEase(time, begin, change, duration) {
    return change * time / duration + begin;
}

function sineEaseIn(time, begin, change, duration) {
    return -change * Math.cos(time / duration * (Math.PI / 2)) + change + begin;
}

function sineEaseOut(time, begin, change, duration) {
    return change * Math.sin(time / duration * (Math.PI / 2)) + begin;
}

function sineEaseInOut(time, begin, change, duration) {
    return -change / 2 * (Math.cos(Math.PI * time / duration) - 1) + begin;
}

function sineEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        return sineEaseIn(time * 2, begin, change / 2, duration);
    }

    return sineEaseOut(time * 2 - duration, begin + change / 2, change / 2, duration);
}

function powerEase(time, begin, change, duration, power) {
    return change * Math.pow(time / duration, power) + begin;
}

function powerEaseInOut(time, begin, change, duration, power) {
    var local = time / (duration / 2);
    if (local < 1) {
        return change / 2 * Math.pow(local, power) + begin;
    }

    return change / 2 * (2 - Math.pow(2 - local, power)) + begin;
}

function quadraticEaseIn(time, begin, change, duration) {
    return powerEase(time, begin, change, duration, 2);
}

function quadraticEaseOut(time, begin, change, duration) {
    return powerEase(duration - time, begin + change, -change, duration, 2);
}

function quadraticEaseInOut(time, begin, change, duration) {
    return powerEaseInOut(time, begin, change, duration, 2);
}

function cubicEaseIn(time, begin, change, duration) {
    return powerEase(time, begin, change, duration, 3);
}

function cubicEaseOut(time, begin, change, duration) {
    return powerEase(duration - time, begin + change, -change, duration, 3);
}

function cubicEaseInOut(time, begin, change, duration) {
    return powerEaseInOut(time, begin, change, duration, 3);
}

function quinticEaseIn(time, begin, change, duration) {
    return powerEase(time, begin, change, duration, 5);
}

function quinticEaseOut(time, begin, change, duration) {
    return powerEase(duration - time, begin + change, -change, duration, 5);
}

function quinticEaseInOut(time, begin, change, duration) {
    return powerEaseInOut(time, begin, change, duration, 5);
}

function exponentialEaseIn(time, begin, change, duration) {
    if (time === 0) {
        return begin;
    }

    return change * Math.pow(2, 10 * (time / duration - 1)) + begin;
}

function exponentialEaseOut(time, begin, change, duration) {
    if (time === duration) {
        return begin + change;
    }

    return change * (1 - Math.pow(2, -10 * time / duration)) + begin;
}

function exponentialEaseInOut(time, begin, change, duration) {
    if (time === 0) {
        return begin;
    }

    if (time === duration) {
        return begin + change;
    }

    var local = time / (duration / 2);
    if (local < 1) {
        return change / 2 * Math.pow(2, 10 * (local - 1)) + begin;
    }

    local = local - 1;
    return change / 2 * (2 - Math.pow(2, -10 * local)) + begin;
}

function circularEaseIn(time, begin, change, duration) {
    var local = time / duration;
    return -change * (Math.sqrt(1 - local * local) - 1) + begin;
}

function circularEaseOut(time, begin, change, duration) {
    var local = time / duration - 1;
    return change * Math.sqrt(1 - local * local) + begin;
}

function circularEaseInOut(time, begin, change, duration) {
    var local = time / (duration / 2);
    if (local < 1) {
        return -change / 2 * (Math.sqrt(1 - local * local) - 1) + begin;
    }

    local = local - 2;
    return change / 2 * (Math.sqrt(1 - local * local) + 1) + begin;
}

var BACK_OVERSHOOT = 1.70158;

function backEaseIn(time, begin, change, duration) {
    var local = time / duration;
    return change * local * local
        * ((BACK_OVERSHOOT + 1) * local - BACK_OVERSHOOT) + begin;
}

function backEaseOut(time, begin, change, duration) {
    var local = time / duration - 1;
    return change * (local * local
        * ((BACK_OVERSHOOT + 1) * local + BACK_OVERSHOOT) + 1) + begin;
}

function backEaseInOut(time, begin, change, duration) {
    var local = time / (duration / 2);
    if (local < 1) {
        return change / 2 * (local * local
            * (((BACK_OVERSHOOT * 1.525) + 1) * local - (BACK_OVERSHOOT * 1.525))) + begin;
    }

    local = local - 2;
    return change / 2 * (local * local
        * (((BACK_OVERSHOOT * 1.525) + 1) * local + (BACK_OVERSHOOT * 1.525)) + 2) + begin;
}

// ---- 原版 M8 缓动补齐（org.libspark.betweenas3.core.easing.*，逐式移植）----
// 宿主原先缺 Quartic 全系、Bounce 全系、Elastic 全系，以及
// Quadratic/Cubic/Quintic/Circular/Exponential/Back 的 easeOutIn；
// 且 Quartic 被错映射到 quintic（四次方被当成五次方）。这里按反编译源码补齐。

function quarticEaseIn(time, begin, change, duration) {
    time = time / duration;
    return change * time * time * time * time + begin;
}

function quarticEaseOut(time, begin, change, duration) {
    time = time / duration - 1;
    return -change * (time * time * time * time - 1) + begin;
}

function quarticEaseInOut(time, begin, change, duration) {
    time = time / (duration / 2);
    if (time < 1) {
        return change / 2 * time * time * time * time + begin;
    }

    time = time - 2;
    return -change / 2 * (time * time * time * time - 2) + begin;
}

function quarticEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        time = time * 2 / duration - 1;
        return -(change / 2) * (time * time * time * time - 1) + begin;
    }

    time = (time * 2 - duration) / duration;
    return change / 2 * time * time * time * time + (begin + change / 2);
}

function bounceEaseIn(time, begin, change, duration) {
    time = (duration - time) / duration;
    if (time < 1 / 2.75) {
        return change - change * (7.5625 * time * time) + begin;
    }

    if (time < 2 / 2.75) {
        time = time - 1.5 / 2.75;
        return change - change * (7.5625 * time * time + 0.75) + begin;
    }

    if (time < 2.5 / 2.75) {
        time = time - 2.25 / 2.75;
        return change - change * (7.5625 * time * time + 0.9375) + begin;
    }

    time = time - 2.625 / 2.75;
    return change - change * (7.5625 * time * time + 0.984375) + begin;
}

function bounceEaseOut(time, begin, change, duration) {
    time = time / duration;
    if (time < 1 / 2.75) {
        return change * (7.5625 * time * time) + begin;
    }

    if (time < 2 / 2.75) {
        time = time - 1.5 / 2.75;
        return change * (7.5625 * time * time + 0.75) + begin;
    }

    if (time < 2.5 / 2.75) {
        time = time - 2.25 / 2.75;
        return change * (7.5625 * time * time + 0.9375) + begin;
    }

    time = time - 2.625 / 2.75;
    return change * (7.5625 * time * time + 0.984375) + begin;
}

function bounceEaseInOut(time, begin, change, duration) {
    var scale;
    if (time < duration / 2) {
        time = (duration - time * 2) / duration;
        if (time < 1 / 2.75) {
            return (change - change * (7.5625 * time * time)) * 0.5 + begin;
        }

        if (time < 2 / 2.75) {
            time = time - 1.5 / 2.75;
            return (change - change * (7.5625 * time * time + 0.75)) * 0.5 + begin;
        }

        if (time < 2.5 / 2.75) {
            time = time - 2.25 / 2.75;
            return (change - change * (7.5625 * time * time + 0.9375)) * 0.5 + begin;
        }

        time = time - 2.625 / 2.75;
        return (change - change * (7.5625 * time * time + 0.984375)) * 0.5 + begin;
    }

    time = (time * 2 - duration) / duration;
    if (time < 1 / 2.75) {
        return change * (7.5625 * time * time) * 0.5 + change * 0.5 + begin;
    }

    if (time < 2 / 2.75) {
        time = time - 1.5 / 2.75;
        scale = 7.5625 * time * time + 0.75;
        return change * scale * 0.5 + change * 0.5 + begin;
    }

    if (time < 2.5 / 2.75) {
        time = time - 2.25 / 2.75;
        scale = 7.5625 * time * time + 0.9375;
        return change * scale * 0.5 + change * 0.5 + begin;
    }

    time = time - 2.625 / 2.75;
    scale = 7.5625 * time * time + 0.984375;
    return change * scale * 0.5 + change * 0.5 + begin;
}

function bounceEaseOutIn(time, begin, change, duration) {
    var scale;
    if (time < duration / 2) {
        time = time * 2 / duration;
        if (time < 1 / 2.75) {
            return change / 2 * (7.5625 * time * time) + begin;
        }

        if (time < 2 / 2.75) {
            time = time - 1.5 / 2.75;
            return change / 2 * (7.5625 * time * time + 0.75) + begin;
        }

        if (time < 2.5 / 2.75) {
            time = time - 2.25 / 2.75;
            return change / 2 * (7.5625 * time * time + 0.9375) + begin;
        }

        time = time - 2.625 / 2.75;
        return change / 2 * (7.5625 * time * time + 0.984375) + begin;
    }

    time = (duration - (time * 2 - duration)) / duration;
    if (time < 1 / 2.75) {
        return change / 2 - change / 2 * (7.5625 * time * time) + (begin + change / 2);
    }

    if (time < 2 / 2.75) {
        time = time - 1.5 / 2.75;
        scale = 7.5625 * time * time + 0.75;
        return change / 2 - change / 2 * scale + (begin + change / 2);
    }

    if (time < 2.5 / 2.75) {
        time = time - 2.25 / 2.75;
        scale = 7.5625 * time * time + 0.9375;
        return change / 2 - change / 2 * scale + (begin + change / 2);
    }

    time = time - 2.625 / 2.75;
    scale = 7.5625 * time * time + 0.984375;
    return change / 2 - change / 2 * scale + (begin + change / 2);
}

// Elastic 原版在实例上缓存周期 p 与振幅 a（首次 calculate 时按 change/duration 定），
// 这里用闭包保留同样的状态。
var elasticEaseIn = (function () {
    var period = 0;
    var amplitude = 0;
    return function (time, begin, change, duration) {
        var shift;
        if (time === 0) {
            return begin;
        }

        time = time / duration;
        if (time === 1) {
            return begin + change;
        }

        if (!period) {
            period = duration * 0.3;
        }

        if (!amplitude || amplitude < Math.abs(change)) {
            amplitude = change;
            shift = period / 4;
        } else {
            shift = period / (2 * Math.PI) * Math.asin(change / amplitude);
        }

        time = time - 1;
        return -(amplitude * Math.pow(2, 10 * time)
            * Math.sin((time * duration - shift) * (2 * Math.PI) / period)) + begin;
    };
}());

var elasticEaseOut = (function () {
    var period = 0;
    var amplitude = 0;
    return function (time, begin, change, duration) {
        var shift;
        if (time === 0) {
            return begin;
        }

        time = time / duration;
        if (time === 1) {
            return begin + change;
        }

        if (!period) {
            period = duration * 0.3;
        }

        if (!amplitude || amplitude < Math.abs(change)) {
            amplitude = change;
            shift = period / 4;
        } else {
            shift = period / (2 * Math.PI) * Math.asin(change / amplitude);
        }

        return amplitude * Math.pow(2, -10 * time)
            * Math.sin((time * duration - shift) * (2 * Math.PI) / period) + change + begin;
    };
}());

var elasticEaseInOut = (function () {
    var period = 0;
    var amplitude = 0;
    return function (time, begin, change, duration) {
        var shift;
        if (time === 0) {
            return begin;
        }

        time = time / (duration / 2);
        if (time === 2) {
            return begin + change;
        }

        if (!period) {
            period = duration * (0.3 * 1.5);
        }

        if (!amplitude || amplitude < Math.abs(change)) {
            amplitude = change;
            shift = period / 4;
        } else {
            shift = period / (2 * Math.PI) * Math.asin(change / amplitude);
        }

        if (time < 1) {
            time = time - 1;
            return -0.5 * (amplitude * Math.pow(2, 10 * time)
                * Math.sin((time * duration - shift) * (2 * Math.PI) / period)) + begin;
        }

        time = time - 1;
        return amplitude * Math.pow(2, -10 * time)
            * Math.sin((time * duration - shift) * (2 * Math.PI) / period) * 0.5 + change + begin;
    };
}());

var elasticEaseOutIn = (function () {
    var period = 0;
    var amplitude = 0;
    return function (time, begin, change, duration) {
        var shift;
        var half = change / 2;
        if (time < duration / 2) {
            time = time * 2;
            if (time === 0) {
                return begin;
            }

            time = time / duration;
            if (time === 1) {
                return begin + half;
            }

            if (!period) {
                period = duration * 0.3;
            }

            if (!amplitude || amplitude < Math.abs(half)) {
                amplitude = half;
                shift = period / 4;
            } else {
                shift = period / (2 * Math.PI) * Math.asin(half / amplitude);
            }

            return amplitude * Math.pow(2, -10 * time)
                * Math.sin((time * duration - shift) * (2 * Math.PI) / period) + half + begin;
        }

        time = time * 2 - duration;
        if (time === 0) {
            return begin + half;
        }

        time = time / duration;
        if (time === 1) {
            return begin + half + half;
        }

        if (!period) {
            period = duration * 0.3;
        }

        if (!amplitude || amplitude < Math.abs(half)) {
            amplitude = half;
            shift = period / 4;
        } else {
            shift = period / (2 * Math.PI) * Math.asin(half / amplitude);
        }

        time = time - 1;
        return -(amplitude * Math.pow(2, 10 * time)
            * Math.sin((time * duration - shift) * (2 * Math.PI) / period)) + (begin + half);
    };
}());

function quadraticEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        time = time * 2 / duration;
        return -(change / 2) * time * (time - 2) + begin;
    }

    time = (time * 2 - duration) / duration;
    return change / 2 * time * time + (begin + change / 2);
}

function cubicEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        time = time * 2 / duration - 1;
        return change / 2 * (time * time * time + 1) + begin;
    }

    time = (time * 2 - duration) / duration;
    return change / 2 * time * time * time + begin + change / 2;
}

function quinticEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        time = time * 2 / duration - 1;
        return change / 2 * (time * time * time * time * time + 1) + begin;
    }

    time = (time * 2 - duration) / duration;
    return change / 2 * time * time * time * time * time + (begin + change / 2);
}

function circularEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        time = time * 2 / duration - 1;
        return change / 2 * Math.sqrt(1 - time * time) + begin;
    }

    time = (time * 2 - duration) / duration;
    return -(change / 2) * (Math.sqrt(1 - time * time) - 1) + (begin + change / 2);
}

function exponentialEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        return time * 2 === duration
            ? begin + change / 2
            : change / 2 * (1 - Math.pow(2, -10 * time * 2 / duration)) + begin;
    }

    return time * 2 - duration === 0
        ? begin + change / 2
        : change / 2 * Math.pow(2, 10 * ((time * 2 - duration) / duration - 1))
            + begin + change / 2;
}

// 原版 Back 的过冲系数固定 1.70158（BackEaseIn/Out/InOut/OutIn 的默认构造参数）。
function backEaseOutIn(time, begin, change, duration) {
    if (time < duration / 2) {
        time = time * 2 / duration - 1;
        return change / 2 * (time * time * (2.70158 * time + 1.70158) + 1) + begin;
    }

    time = (time * 2 - duration) / duration;
    return change / 2 * time * time * (2.70158 * time - 1.70158) + (begin + change / 2);
}

// ---- M8 的 TweenEasing（原版 BetweenAS3TweenEasing.as:25-59）----
// 18 个类名 → { easeIn, easeOut, easeInOut, easeOutIn }。
// 原版每个常量都是 IEasing 实例（有 calculate），所以这里给包装函数挂 calculate
// 指向自身，脚本写 TweenEasing.Cubic.easeOut.calculate(t,b,c,d) 也等价可用。
function easingFunction(fn) {
    var wrapper = function (time, begin, change, duration) {
        return fn(time, begin, change, duration);
    };
    wrapper.calculate = wrapper;
    return wrapper;
}

function easingGroup(easeIn, easeOut, easeInOut, easeOutIn) {
    return {
        easeIn: easingFunction(easeIn),
        easeOut: easingFunction(easeOut),
        easeInOut: easingFunction(easeInOut),
        easeOutIn: easingFunction(easeOutIn)
    };
}

var linearEasingGroup = easingGroup(linearEase, linearEase, linearEase, linearEase);
var sineEasingGroup = easingGroup(sineEaseIn, sineEaseOut, sineEaseInOut, sineEaseOutIn);
var quadraticEasingGroup = easingGroup(
    quadraticEaseIn, quadraticEaseOut, quadraticEaseInOut, quadraticEaseOutIn);
var cubicEasingGroup = easingGroup(cubicEaseIn, cubicEaseOut, cubicEaseInOut, cubicEaseOutIn);
var quarticEasingGroup = easingGroup(quarticEaseIn, quarticEaseOut, quarticEaseInOut, quarticEaseOutIn);
var quinticEasingGroup = easingGroup(quinticEaseIn, quinticEaseOut, quinticEaseInOut, quinticEaseOutIn);
var exponentialEasingGroup = easingGroup(
    exponentialEaseIn, exponentialEaseOut, exponentialEaseInOut, exponentialEaseOutIn);
var circularEasingGroup = easingGroup(
    circularEaseIn, circularEaseOut, circularEaseInOut, circularEaseOutIn);
var backEasingGroup = easingGroup(backEaseIn, backEaseOut, backEaseInOut, backEaseOutIn);
var bounceEasingGroup = easingGroup(bounceEaseIn, bounceEaseOut, bounceEaseInOut, bounceEaseOutIn);
var elasticEasingGroup = easingGroup(elasticEaseIn, elasticEaseOut, elasticEaseInOut, elasticEaseOutIn);

// 原版 Physical 是按帧率模拟的物理缓动（IPhysicalEasing），不是时间轴缓动：
// calculate(t, b, c) 的第三参是位移量，另配 getDuration 求时长。
// 默认帧率沿用原版 Physical.as 的 _defaultFrameRate = 60。
var DEFAULT_PHYSICAL_FRAME_RATE = 30;

function physicalFrameRate(fps) {
    return isNaN(Number(fps)) ? DEFAULT_PHYSICAL_FRAME_RATE : Number(fps);
}

function physicalEasing(calculate, getDuration) {
    var wrapper = function (time, begin, change) {
        return calculate(time, begin, change);
    };
    wrapper.calculate = wrapper;
    wrapper.getDuration = getDuration;
    return wrapper;
}

var TweenEasing = {
    Back: backEasingGroup,
    Bounce: bounceEasingGroup,
    Circ: circularEasingGroup,
    Circular: circularEasingGroup,
    Cubic: cubicEasingGroup,
    // 原版 Custom.func(f) 用自定义函数构造缓动（CustomFunctionEasing）。
    Custom: {
        func: function (fn) {
            return easingFunction(fn);
        }
    },
    Elastic: elasticEasingGroup,
    Expo: exponentialEasingGroup,
    Exponential: exponentialEasingGroup,
    Linear: linearEasingGroup,
    Physical: {
        uniform: function (speed, fps) {
            var v = speed === undefined ? 10 : Number(speed);
            var rate = physicalFrameRate(fps);
            return physicalEasing(
                function (time, begin, change) {
                    return begin + (change < 0 ? -v : v) * (time / (1 / rate));
                },
                function (distance) {
                    return distance / (distance < 0 ? -v : v) * (1 / rate);
                });
        },
        exponential: function (rate, threshold, fps) {
            var f = rate === undefined ? 0.2 : Number(rate);
            var th = threshold === undefined ? 0.0001 : Number(threshold);
            var frameRate = physicalFrameRate(fps);
            return physicalEasing(
                function (time, begin, change) {
                    return -change * Math.pow(1 - f, time / (1 / frameRate) - 1) + (begin + change);
                },
                function (distance) {
                    return (Math.log(th / distance) / Math.log(1 - f) + 1) * (1 / frameRate);
                });
        },
        accelerate: function (accel, velocity, fps) {
            var a = accel === undefined ? 1 : Number(accel);
            var iv = velocity === undefined ? 0 : Number(velocity);
            var frameRate = physicalFrameRate(fps);
            return physicalEasing(
                function (time, begin, change) {
                    var sign = change < 0 ? -1 : 1;
                    var step = time / (1 / frameRate);
                    return begin + sign * iv * step + sign * a * step * step / 2;
                },
                function (distance) {
                    var v = distance < 0 ? -iv : iv;
                    var acc = distance < 0 ? -a : a;
                    return (-v + Math.sqrt(v * v - 4 * (acc / 2) * -distance))
                        / (2 * (acc / 2)) * (1 / frameRate);
                });
        }
    },
    Quad: quadraticEasingGroup,
    Quadratic: quadraticEasingGroup,
    Quart: quarticEasingGroup,
    Quartic: quarticEasingGroup,
    Quint: quinticEasingGroup,
    Quintic: quinticEasingGroup,
    Sine: sineEasingGroup
};

var easingTable = {
    EaseNone: linearEase,
    Linear: linearEase,
    LinearEaseIn: linearEase,
    LinearEaseOut: linearEase,
    LinearEaseInOut: linearEase,
    LinearEaseOutIn: linearEase,
    SineEaseIn: sineEaseIn,
    SineEaseOut: sineEaseOut,
    SineEaseInOut: sineEaseInOut,
    SineEaseOutIn: sineEaseOutIn,
    QuadraticEaseIn: quadraticEaseIn,
    QuadraticEaseOut: quadraticEaseOut,
    QuadraticEaseInOut: quadraticEaseInOut,
    CubicEaseIn: cubicEaseIn,
    CubicEaseOut: cubicEaseOut,
    CubicEaseInOut: cubicEaseInOut,
    QuarticEaseIn: quarticEaseIn,
    QuarticEaseOut: quarticEaseOut,
    QuarticEaseInOut: quarticEaseInOut,
    QuarticEaseOutIn: quarticEaseOutIn,
    QuinticEaseIn: quinticEaseIn,
    QuinticEaseOut: quinticEaseOut,
    QuinticEaseInOut: quinticEaseInOut,
    ExponentialEaseIn: exponentialEaseIn,
    ExponentialEaseOut: exponentialEaseOut,
    ExponentialEaseInOut: exponentialEaseInOut,
    CircularEaseIn: circularEaseIn,
    CircularEaseOut: circularEaseOut,
    CircularEaseInOut: circularEaseInOut,
    BackEaseIn: backEaseIn,
    BackEaseOut: backEaseOut,
    BackEaseInOut: backEaseInOut,
    BackEaseOutIn: backEaseOutIn,
    QuinticEaseOutIn: quinticEaseOutIn,
    QuadraticEaseOutIn: quadraticEaseOutIn,
    CubicEaseOutIn: cubicEaseOutIn,
    CircularEaseOutIn: circularEaseOutIn,
    ExponentialEaseOutIn: exponentialEaseOutIn,
    BounceEaseIn: bounceEaseIn,
    BounceEaseOut: bounceEaseOut,
    BounceEaseInOut: bounceEaseInOut,
    BounceEaseOutIn: bounceEaseOutIn,
    ElasticEaseIn: elasticEaseIn,
    ElasticEaseOut: elasticEaseOut,
    ElasticEaseInOut: elasticEaseInOut,
    ElasticEaseOutIn: elasticEaseOutIn
};

// 原版 MotionManager 的 switch 认的是**缓动类名**（MotionManager.as:205-239）：
// None / Back / Bounce / Circular / Cubic / Elastic / Exponential /
// Sine / Quintic / Linear，命中后取该类的 easeInOut。这张表把它们
// 落到 easingTable 里的全名条目上——缺了它，脚本写 `easing: "Sine"`
// 会静默退化成线性（原版是 Sine.easeInOut），缓动看起来「没生效」。
var M8_EASING_CLASS_NAMES = {
    None: linearEase,
    Linear: linearEase,
    Back: backEaseInOut,
    Bounce: bounceEaseInOut,
    Circular: circularEaseInOut,
    Cubic: cubicEaseInOut,
    Elastic: elasticEaseInOut,
    Exponential: exponentialEaseInOut,
    Sine: sineEaseInOut,
    Quintic: quinticEaseInOut
};

function resolveEasing(value) {
    if (typeof value === "function") {
        return value;
    }

    if (typeof value !== "string" || !value) {
        return linearEase;
    }

    // 允许 "M8Easing.SineEaseInOut" 这类带命名空间的全名。
    var dot = value.lastIndexOf(".");
    var name = dot >= 0 ? value.substring(dot + 1) : value;
    // 未知名的兜底保持线性：原版那两个 switch 的兜底是 Sine.easeInOut，
    // 但它的输入在此之前已被 initTween 填成 "Linear"，
    // 真能撞上「未知名」的只有直接传 IEasing 对象的 Tween.tween，
    // 那里不会是字符串。所以这里不跟着抄 Sine。
    return easingTable[name] || M8_EASING_CLASS_NAMES[name] || linearEase;
}

export {
    TweenEasing,
    resolveEasing
};
