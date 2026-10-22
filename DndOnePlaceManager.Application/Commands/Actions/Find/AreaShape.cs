namespace DndOnePlaceManager.Application.Commands.Actions.Find
{
    /// <summary>
    /// An area on a map, in map pixels: a circle or rect centred on X, Y, or a line or cone
    /// starting at X, Y and pointing at Toward. Radius is a circle's radius or a line's /
    /// cone's length; Width is a rect's or a line's width; Angle is a cone's spread in degrees.
    /// </summary>
    public sealed record Area(string Shape, double X, double Y, double Radius, double Width, double Height, double Angle,
        double? TowardX, double? TowardY);

    /// <summary>
    /// What an area covers — the one rule for Find, RequestPoint and (in the client's copy,
    /// areaShape.js, tested on the same area-cases.json) the highlighting on the map: a token
    /// counts when the circle inside it (radius half its shorter side) touches the shape.
    /// For a circle that is "within the radius, counted to the token's edge".
    /// </summary>
    public static class AreaShape
    {
        public static readonly string[] Shapes = { "circle", "rect", "line", "cone" };

        // Rounding (snapping, pixels) shouldn't decide a token that exactly touches.
        private const double Epsilon = 1e-6;

        public static bool TouchesToken(Area area, double x, double y, double w, double h) =>
            Distance(area, x, y) <= Math.Min(w, h) / 2 + Epsilon;

        /// <summary>How far a point is from the area; 0 inside it.</summary>
        public static double Distance(Area area, double px, double py)
        {
            switch (area.Shape?.Trim().ToLowerInvariant())
            {
                case "circle":
                    return Math.Max(0, Math.Sqrt(Sq(px - area.X) + Sq(py - area.Y)) - area.Radius);
                case "rect":
                    var dx = Math.Max(0, Math.Abs(px - area.X) - area.Width / 2);
                    var dy = Math.Max(0, Math.Abs(py - area.Y) - area.Height / 2);
                    return Math.Sqrt(dx * dx + dy * dy);
                case "line":
                case "cone":
                    return PolygonDistance(Polygon(area), px, py);
                default:
                    throw new ArgumentException($"An area is {string.Join(", ", Shapes)}, not '{area.Shape}'.");
            }
        }

        /// <summary>A line's or cone's corners (a cone as the map draws it: a triangle).</summary>
        public static (double X, double Y)[] Polygon(Area area)
        {
            if (area.TowardX is not double tx || area.TowardY is not double ty || (tx == area.X && ty == area.Y))
                throw new ArgumentException($"A {area.Shape} needs a direction: a point or token it points at (Toward).");
            var dir = Math.Atan2(ty - area.Y, tx - area.X);
            var length = area.Radius;

            if (string.Equals(area.Shape, "cone", StringComparison.OrdinalIgnoreCase))
            {
                var half = area.Angle * Math.PI / 360;
                return new[]
                {
                    (area.X, area.Y),
                    (area.X + length * Math.Cos(dir - half), area.Y + length * Math.Sin(dir - half)),
                    (area.X + length * Math.Cos(dir + half), area.Y + length * Math.Sin(dir + half)),
                };
            }

            double ux = Math.Cos(dir), uy = Math.Sin(dir), nx = -uy * area.Width / 2, ny = ux * area.Width / 2;
            double ex = area.X + ux * length, ey = area.Y + uy * length;
            return new[] { (area.X + nx, area.Y + ny), (ex + nx, ey + ny), (ex - nx, ey - ny), (area.X - nx, area.Y - ny) };
        }

        private static double PolygonDistance((double X, double Y)[] polygon, double px, double py)
        {
            if (Inside(polygon, px, py))
                return 0;
            var best = double.MaxValue;
            for (var i = 0; i < polygon.Length; i++)
                best = Math.Min(best, SegmentDistance(polygon[i], polygon[(i + 1) % polygon.Length], px, py));
            return best;
        }

        // Convex polygons only (rects, lines, cones): the point is on the same side of every edge.
        private static bool Inside((double X, double Y)[] polygon, double px, double py)
        {
            int sign = 0;
            for (var i = 0; i < polygon.Length; i++)
            {
                var (ax, ay) = polygon[i];
                var (bx, by) = polygon[(i + 1) % polygon.Length];
                var cross = (bx - ax) * (py - ay) - (by - ay) * (px - ax);
                var s = Math.Sign(cross);
                if (s == 0) continue;
                if (sign == 0) sign = s;
                else if (s != sign) return false;
            }
            return true;
        }

        private static double SegmentDistance((double X, double Y) a, (double X, double Y) b, double px, double py)
        {
            double vx = b.X - a.X, vy = b.Y - a.Y;
            var lengthSq = vx * vx + vy * vy;
            var t = lengthSq == 0 ? 0 : Math.Clamp(((px - a.X) * vx + (py - a.Y) * vy) / lengthSq, 0, 1);
            return Math.Sqrt(Sq(px - (a.X + t * vx)) + Sq(py - (a.Y + t * vy)));
        }

        private static double Sq(double v) => v * v;
    }
}
