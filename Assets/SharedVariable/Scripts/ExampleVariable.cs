using TMPro;
using UnityEngine;

namespace SharedVariable.Scripts
{
    public class ExampleVariable : MonoBehaviour
    {
        [SerializeField] private IntVariable health;
        [SerializeField] private TextMeshProUGUI healthText;

        private void Start()
        {
            health.AddListener(UpdateHealthText);
            UpdateHealthText(health.Value); // 초기값 표시
        }

        private void UpdateHealthText(int value)
        {
            healthText.text = $"Health: {value}";
        }
    }
}