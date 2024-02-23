﻿namespace Rebus.Firebird;

internal static class MathExtensions
{
	public static int RoundUpToNextPowerOfTwo(this int number) => 1 << (int)Math.Ceiling(Math.Log(number, newBase: 2));
}
