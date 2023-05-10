using System;

namespace Adventure.Logic.Data {
	public static class Interpolation {
		
		public delegate float Interpolator(float a);
		
		const float EPSILON = 0.0001F;
		
		public static float Linear(float a) {
			return a;
		}

		public static float Smooth(float a) {
			return a * a * (3 - 2 * a);
		}

		public static float Smooth2(float a) {
			a = a * a * (3 - 2 * a);
			return a * a * (3 - 2 * a);
		}

		public static float Smoother(float a) {
			return a * a * a * (a * (a * 6 - 15) + 10);
		}

		public static float Pow2(float a) {
			if (a <= 0.5f) return MathF.Pow(a * 2, 2) / 2;
			return MathF.Pow((a - 1) * 2, 2) / -2 + 1;
		}

		public static float Pow2In(float a) {
			return MathF.Pow(a, 2);
		}

		public static float Pow2Out(float a) {
			return MathF.Pow(a - 1, 2) * -1 + 1;
		}

		public static float Pow2InInverse(float a) {
			if (a < EPSILON) return 0;
			return MathF.Sqrt(a);
		}

		public static float Pow2OutInverse(float a) {
			if (a < EPSILON) return 0;
			if (a > 1) return 1;
			return 1 - MathF.Sqrt(-(a - 1));
		}

		public static float Pow3(float a) {
			if (a <= 0.5f) return MathF.Pow(a * 2, 3) / 2;
			return MathF.Pow((a - 1) * 2, 3) / 2 + 1;
		}

		public static float Pow3In(float a) {
			return MathF.Pow(a, 3);
		}

		public static float Pow3Out(float a) {
			return MathF.Pow(a - 1, 3) * 1 + 1;
		}

		public static float Pow3InInverse(float a) {
			return MathF.Cbrt(a);
		}

		public static float Pow3OutInverse(float a) {
			return 1 - MathF.Cbrt(-(a - 1));
		}

		public static float Pow4(float a) {
			if (a <= 0.5f) return MathF.Pow(a * 2, 4) / 2;
			return MathF.Pow((a - 1) * 2, 4) / -2 + 1;
		}

		public static float Pow4In(float a) {
			return MathF.Pow(a, 4);
		}

		public static float Pow4Out(float a) {
			return MathF.Pow(a - 1, 4) * -1 + 1;
		}

		public static float Pow5(float a) {
			if (a <= 0.5f) return MathF.Pow(a * 2, 5) / 2;
			return MathF.Pow((a - 1) * 2, 5) / 2 + 1;
		}

		public static float Pow5In(float a) {
			return MathF.Pow(a, 5);
		}

		public static float Pow5Out(float a) {
			return MathF.Pow(a - 1, 5) * 1 + 1;
		}

		public static float Exp10(float a) {
			const float value = 2;
			const float power = 10;
			const float min = 0.0009765625f;//MathF.Pow(value, -power);
			const float scale = 1.000978f;//1 / (1 - min);

			if (a <= 0.5f) return (MathF.Pow(value, power * (a * 2 - 1)) - min) * scale / 2;
			return (2 - (MathF.Pow(value, -power * (a * 2 - 1)) - min) * scale) / 2;
		}

		public static float Exp10In(float a) {
			const float value = 2;
			const float power = 10;
			const float min = 0.0009765625f;//MathF.Pow(value, -power);
			const float scale = 1.000978f;//1 / (1 - min);

			return (MathF.Pow(value, power * (a - 1)) - min) * scale;
		}

		public static float Exp10Out(float a) {
			const float value = 2;
			const float power = 10;
			const float min = 0.0009765625f;//MathF.Pow(value, -power);
			const float scale = 1.000978f;//1 / (1 - min);

			return 1 - (MathF.Pow(value, -power * a) - min) * scale;
		}

		public static float Exp5(float a) {
			const float value = 2;
			const float power = 5;
			const float min = 0.03125f;//MathF.Pow(value, -power);
			const float scale = 1.032258f;//1 / (1 - min);

			if (a <= 0.5f) return (MathF.Pow(value, power * (a * 2 - 1)) - min) * scale / 2;
			return (2 - (MathF.Pow(value, -power * (a * 2 - 1)) - min) * scale) / 2;
		}

		public static float Exp5In(float a) {
			const float value = 2;
			const float power = 5;
			const float min = 0.03125f;//MathF.Pow(value, -power);
			const float scale = 1.032258f;//1 / (1 - min);

			return (MathF.Pow(value, power * (a - 1)) - min) * scale;
		}

		public static float Exp5Out(float a) {
			const float value = 2;
			const float power = 5;
			const float min = 0.03125f;//MathF.Pow(value, -power);
			const float scale = 1.032258f;//1 / (1 - min);

			return 1 - (MathF.Pow(value, -power * a) - min) * scale;
		}

		public static float Sine(float a) {
			return (1 - MathF.Cos(a * MathF.PI)) / 2;
		}

		public static float SineIn(float a) {
			return 1 - MathF.Cos(a * MathF.PI / 2);
		}

		public static float SineOut(float a) {
			return MathF.Sin(a * MathF.PI / 2);
		}

		public static float CircleReverse(float a) {
			if (a <= 0.5f) {
				return CircleOut(a) / 2;
			} else {
				return CircleIn(a) / 2 + 0.5f;
			}
		}

		public static float Circle(float a) {
			if (a <= 0.5f) {
				a *= 2;
				return (1 - MathF.Sqrt(1 - a * a)) / 2;
			}

			a--;
			a *= 2;
			return (MathF.Sqrt(1 - a * a) + 1) / 2;
		}

		public static float CircleIn(float a) {
			return 1 - MathF.Sqrt(1 - a * a);
		}

		public static float CircleOut(float a) {
			a--;
			return MathF.Sqrt(1 - a * a);
		}

		public static float Bezier(float x1, float y1, float x2, float y2, float a) {
			return new BezierEasing(x1, y1, x2, y2).ease(a);
		}

		private struct BezierEasing {
			private float x1;
			private float y1;
			private float x2;
			private float y2;
			private float ax;
			private float ay;
			private float bx;
			private float by;
			private float cx;
			private float cy;

			public BezierEasing(float x1, float y1, float x2, float y2) : this() {
				this.x1 = x1 < 0 ? 0 : x1 > 1 ? 1 : x1;
				this.y1 = y1 < 0 ? 0 : y1 > 1 ? 1 : y1;
				this.x2 = x2 < 0 ? 0 : x2 > 1 ? 1 : x2;
				this.y2 = y2 < 0 ? 0 : y2 > 1 ? 1 : y2;
			}
			
			public float ease(float time) {
				return getBezierCoordinateY(getXForTime(time));
			}
			
			private float getBezierCoordinateY(float time) {
				cy = 3 * y1;
				by = 3 * (y2 - y1) - cy;
				ay = 1 - cy - by;
				return time * (cy + time * (by + time * ay));
			}

			private float getXForTime(float time) {
				float x = time;
				for (int i = 1; i < 14; i++) {
					float z = getBezierCoordinateX(x) - time;
					if (Math.Abs(z) < 1e-3) {
						break;
					}
					x -= z / getXDerivate(x);
				}
				return x;
			}

			private float getXDerivate(float t) {
				return cx + t * (2 * bx + 3 * ax * t);
			}

			private float getBezierCoordinateX(float time) {
				cx = 3 * x1;
				bx = 3 * (x2 - x1) - cx;
				ax = 1 - cx - bx;
				return time * (cx + time * (bx + time * ax));
			}
		}
	}
}