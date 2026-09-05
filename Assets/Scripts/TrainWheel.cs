using UnityEngine;

namespace TrainStation
{
    /// <summary>
    /// One wheel, which knows its own radius.
    ///
    /// A steam locomotive's driving wheels are nearly twice the diameter of the carriage wheels
    /// behind it, so rolling every wheel at one shared rate would leave half the train visibly
    /// skating. Each wheel converting metres to degrees for itself removes that whole class of bug.
    /// </summary>
    public class TrainWheel : MonoBehaviour
    {
        [Tooltip("Radius in metres, as built. This is what converts distance into rotation.")]
        public float radius = 0.6f;

        /// <summary>Roll forward by <paramref name="metres"/>, without slipping.</summary>
        public void Roll(float metres)
        {
            if (radius <= 0.0001f) return;

            // The wheel pivots are laid on their side, so the axle is their local Y.
            float degrees = metres / (2f * Mathf.PI * radius) * 360f;
            transform.Rotate(0f, -degrees, 0f, Space.Self);
        }
    }
}
