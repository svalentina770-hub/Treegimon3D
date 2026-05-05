using UnityEngine;


public class ConfigManager : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelConfiguracion;
    public GameObject panelSonido;

   
    public void AbrirConfiguracion()
    {
        panelConfiguracion.SetActive(true);
        Time.timeScale = 0f; // Pausa el juego
    }

    
    public void Reanudar()
    {
        panelConfiguracion.SetActive(false);
        panelSonido.SetActive(false);
        Time.timeScale = 1f; 
    }

    
    public void AbrirSonido()
    {
        panelConfiguracion.SetActive(false);
        panelSonido.SetActive(true);
    }

   
    public void VolverAConfiguracion()
    {
        panelSonido.SetActive(false);
        panelConfiguracion.SetActive(true);
    }


    public void Salir()
    {
        Debug.Log("Cerrando juego...");
        Application.Quit();
    }

    public void CerrarConfiguracion()
    {
        panelConfiguracion.SetActive(false);
        panelSonido.SetActive(false);
        Time.timeScale = 1f;
    }
}