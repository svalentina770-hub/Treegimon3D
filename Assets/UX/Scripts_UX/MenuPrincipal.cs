using UnityEngine;
using UnityEngine.SceneManagement; // Necesario para cambiar de escena
using TMPro; // Necesario si usas TextMeshPro

public class MenuPrincipal : MonoBehaviour
{
    public TMP_InputField inputNombre; // Arrastra aquí tu Input Field

    public void EmpezarJuego()
    {
        // 1. Guardar el nombre del jugador
        string nombreJugador = inputNombre.text;

        if (!string.IsNullOrEmpty(nombreJugador))
        {
            // Guardado persistente (se queda grabado aunque cierres el juego)
            PlayerPrefs.SetString("NombreUsuario", nombreJugador);
            Debug.Log("Nombre guardado: " + nombreJugador);
        }

        // 2. Cambiar a la escena principal
        // Asegúrate de que el nombre de la escena sea EXACTAMENTE igual
        SceneManager.LoadScene("MainScene");
    }
}