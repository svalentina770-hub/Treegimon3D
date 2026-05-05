using UnityEngine;

public class MenuUIManager : MonoBehaviour
{
    [Header("Paneles")]
    public GameObject panelMenu;
    public GameObject panelPlanta;
    public GameObject panelInventario;
    public GameObject panelAyuda;
    public GameObject panelMisiones;


    public void AbrirMenu()
    {
        panelMenu.SetActive(true);
        Time.timeScale = 0f; 
    }


    public void CerrarMenu()
    {
        panelMenu.SetActive(false);
        panelPlanta.SetActive(false);
        panelInventario.SetActive(false);
        panelAyuda.SetActive(false);
        panelMisiones.SetActive(false);

        Time.timeScale = 1f; 
    }


    public void AbrirPlanta()
    {
        panelMenu.SetActive(false);
        panelPlanta.SetActive(true);
    }


    public void AbrirInventario()
    {
        panelMenu.SetActive(false);
        panelInventario.SetActive(true);
    }


    public void AbrirAyuda()
    {
        panelMenu.SetActive(false);
        panelAyuda.SetActive(true);
    }

    public void AbrirMisiones()
    {
        panelMenu.SetActive(false);
        panelMisiones.SetActive(true);
    }


    public void VolverAlMenu()
    {
        panelMenu.SetActive(true);
        panelPlanta.SetActive(false);
        panelInventario.SetActive(false);
        panelAyuda.SetActive(false);
        panelMisiones.SetActive(false);
    }
}