using SCJam.AudioSystem;
using UnityEngine;

namespace SCJam.Core
{
    public class GameManager : MonoSingleton<GameManager>
    {
        [SerializeField] private SoundSO _mainBGM;


        private void Start()
        {
            AudioManager.Instance.PlaySound(_mainBGM);
        }
    }
}
