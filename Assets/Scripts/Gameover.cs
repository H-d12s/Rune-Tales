using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class Gameover : MonoBehaviour
{
public void PlayGames()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex - 2);
    }

    public void QuitGames()
    {
        Application.Quit();
        Debug.Log("QUIT!");
    }
}

