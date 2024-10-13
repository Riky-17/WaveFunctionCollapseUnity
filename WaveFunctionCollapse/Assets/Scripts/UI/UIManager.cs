using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] WFC wfc;

    [SerializeField] TextMeshProUGUI xSizeText;
    [SerializeField] Slider xSizeSlider;
    [SerializeField] TextMeshProUGUI ySizeText;
    [SerializeField] Slider ySizeSlider;
    [SerializeField] TextMeshProUGUI tilesAmountText;
    [SerializeField] Button generateButton;
    [SerializeField] TextMeshProUGUI timeToGenerate;

    void Awake()
    {
        xSizeSlider.value = wfc.gridSizeX;
        ySizeSlider.value = wfc.gridSizeY;
        UpdateUI();
        
        xSizeSlider.onValueChanged.AddListener((float value) => 
        {
            wfc.gridSizeX = value;
            UpdateUI();
        });

        ySizeSlider.onValueChanged.AddListener((float value) => 
        {
            wfc.gridSizeY = value;
            UpdateUI();
        });

        generateButton.onClick.AddListener(() =>
        {
            wfc.WaveFunctionCollapse();
            timeToGenerate.text = "Time to Generate:\r\n" + wfc.timeToGenerate + " seconds";
        });
    }

    void UpdateUI()
    {
        xSizeText.text = "X Size: " + wfc.gridSizeX;
        ySizeText.text = "Y Size: " + wfc.gridSizeY;
        tilesAmountText.text = "Tiles Amount: " + (wfc.NodesAmountX * wfc.NodesAmountY);
    }
}
