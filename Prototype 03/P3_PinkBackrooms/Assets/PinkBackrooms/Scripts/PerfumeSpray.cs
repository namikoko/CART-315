using UnityEngine;

namespace PinkBackrooms
{
    /// A puff of mist from the bottle. The sound and the words come from the Interactable.
    public class PerfumeSpray : MonoBehaviour
    {
        public ParticleSystem mist;

        /// Hooked to the perfume's Interactable.
        public void Spray()
        {
            if (!mist) return;
            if (!mist.isPlaying) mist.Play();
            mist.Emit(28);
            Invoke(nameof(Second), 0.4f); // two pumps, like the sound
        }

        private void Second() { if (mist) mist.Emit(34); }
    }
}
