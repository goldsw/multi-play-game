using UnityEngine;

internal static class PlayerCollisionSweep
{
    public static bool Overlaps(Vector3 first, Vector3 second, Vector3 combinedHalfExtents)
    {
        Vector3 difference = first - second;
        return Mathf.Abs(difference.x) < combinedHalfExtents.x
            && Mathf.Abs(difference.y) < combinedHalfExtents.y
            && Mathf.Abs(difference.z) < combinedHalfExtents.z;
    }

    public static bool TryHit(
        Vector3 firstStart, Vector3 firstDelta, Vector3 secondStart, Vector3 secondDelta,
        Vector3 combinedHalfExtents, out float time, out Vector3 normal)
    {
        Vector3 difference = firstStart - secondStart;
        Vector3 relativeDelta = firstDelta - secondDelta;
        float entry = float.NegativeInfinity;
        float exit = float.PositiveInfinity;
        normal = Vector3.zero;

        for (int axis = 0; axis < 3; axis++)
        {
            float offset = difference[axis];
            float delta = relativeDelta[axis];
            float extent = combinedHalfExtents[axis];
            if (delta == 0f)
            {
                if (Mathf.Abs(offset) >= extent)
                {
                    time = 0f;
                    return false;
                }
                continue;
            }

            float near = (-extent - offset) / delta;
            float far = (extent - offset) / delta;
            if (near > far)
                (near, far) = (far, near);

            if (near > entry)
            {
                entry = near;
                normal = Vector3.zero;
                normal[axis] = delta > 0f ? -1f : 1f;
            }
            exit = Mathf.Min(exit, far);
        }

        time = Mathf.Max(0f, entry);
        return entry >= 0f && time <= 1f && time < exit;
    }
}
