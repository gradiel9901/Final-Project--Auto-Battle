using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections.Generic;
using AI;
using AI.Core;

public class GameManager : MonoBehaviour
{
    [Header("UI References")]
    public TextMeshProUGUI redTeamCountTMP;
    public TextMeshProUGUI blueTeamCountTMP; // User's screenshot calls it blue, even if team is green
    
    [Header("Victory UI")]
    public GameObject victoryPanel;
    public TextMeshProUGUI teamVictoryText;
    public Button restartButton;

    private List<AIAgent> allAgents = new List<AIAgent>();
    private bool gameOver = false;

    private void Start()
    {
        // Hide panel at start
        if (victoryPanel != null) victoryPanel.SetActive(false);

        // Bind restart button
        if (restartButton != null)
        {
            restartButton.onClick.AddListener(RestartGame);
        }

        // Find all agents in the scene at start
        AIAgent[] foundAgents = FindObjectsByType<AIAgent>(FindObjectsSortMode.None);
        allAgents.AddRange(foundAgents);
        
        CheckWinCondition();
    }

    private void Update()
    {
        if (gameOver) return;

        // Check counts every frame (or could run on an interval for performance)
        CheckWinCondition();
    }

    private void CheckWinCondition()
    {
        int redCount = 0;
        int greenBlueCount = 0;

        // Tally alive units
        for (int i = 0; i < allAgents.Count; i++)
        {
            if (allAgents[i] != null && !allAgents[i].IsDead)
            {
                if (allAgents[i].team == Team.Red)
                {
                    redCount++;
                }
                else
                {
                    greenBlueCount++;
                }
            }
        }

        // Update Text
        if (redTeamCountTMP != null) redTeamCountTMP.text = $"Red Team: {redCount}";
        if (blueTeamCountTMP != null) blueTeamCountTMP.text = $"Blue Team: {greenBlueCount}";

        // Win Logic
        if (redCount <= 0 && greenBlueCount > 0)
        {
            TriggerVictory("BLUE TEAM WINS!", Color.blue);
        }
        else if (greenBlueCount <= 0 && redCount > 0)
        {
            TriggerVictory("RED TEAM WINS!", Color.red);
        }
        else if (redCount <= 0 && greenBlueCount <= 0 && allAgents.Count > 0)
        {
            // Rare draw scenario
            TriggerVictory("DRAW!", Color.gray);
        }
    }

    private void TriggerVictory(string message, Color color)
    {
        gameOver = true;
        
        if (victoryPanel != null)
        {
            victoryPanel.SetActive(true);
        }

        if (teamVictoryText != null)
        {
            teamVictoryText.text = message;
            teamVictoryText.color = color;
        }
    }

    public void RestartGame()
    {
        // Reload current scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
