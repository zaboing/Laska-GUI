﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using Laska;

public class GameField : MonoBehaviour
{
    public GameObject BlackPrefab;
    public GameObject WhitePrefab;

    public GameObject SoldierPrefab;
    public GameObject OfficerPrefab;

    public GameObject IngameGUI;
    public GameObject EndGUI;

    public Material BlackMaterial;
    public Material WhiteMaterial;

    public Text WhiteMessage;
    public Text BlackMessage;

    public Text Winner;

    private Board board;

    private Color lastColor;
    private GameObject selected;

    private ArtificialPlayer ai;

    public void Start()
    {
        board = new Board();
        board.Init();
        if (GameLoader.GameMode.ToLower() == "ai")
        {
            ai = new ArtificialPlayer();
        }
        for (int i = 0; i < 7; i++)
        {
            for (int j = 0; j < 7; j++)
            {
                GameObject fieldObject;
                if ((i + j) % 2 == 0)
                {
                    fieldObject = Instantiate(WhitePrefab) as GameObject;
                    fieldObject.transform.position = new Vector3(i, 0, j) + new Vector3(.5f, .2f, .5f);
                    fieldObject.transform.parent = transform;
                    var field = fieldObject.GetComponent<WhiteField>();
                    field.IndexX = i;
                    field.IndexY = j;
                    field.SetGameField(this);
                }
                else
                {
                    fieldObject = Instantiate(BlackPrefab) as GameObject;
                    fieldObject.transform.position = new Vector3(i, 0, j) + new Vector3(.5f, .2f, .5f);
                    fieldObject.transform.parent = transform;
                }
            }
        }
        UpdateAllTowers();
    }

    public Tower this[int i, int j]
    {
        get
        {
            return board[new Pos((byte)j, (byte)i)];
        }
    }

    public void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, LayerMask.GetMask("WhiteField", "Counter")))
            {
                var hitObject = hit.collider.gameObject;
                if (hitObject.layer == LayerMask.NameToLayer("Counter"))
                {
                    Select(hitObject.transform.parent.gameObject);
                }
                else if (selected)
                {
                    var a = selected.transform.parent.parent.GetComponent<WhiteField>();
                    var b = hitObject.GetComponent<WhiteField>();
                    var move = extrapolateMove(new Pos((byte)a.IndexY, (byte)a.IndexX), 
                        new Pos((byte)b.IndexY, (byte)b.IndexX));
                    if (move != null)
                    {
                        board = board.doMove(move);
                        StartCoroutine(Move(move));
                    }
                }
            }
        }
        if (board.Turn == Colour.White)
        {
            BlackMessage.enabled = false;
            WhiteMessage.enabled = true;
        }
        else
        {
            BlackMessage.enabled = true;
            WhiteMessage.enabled = false;
        }
    }

    public void UpdateAllTowers()
    {
        for (int i = transform.childCount - 1; i >= 0; --i)
        {
            var field = transform.GetChild(i).GetComponent<WhiteField>();
            if (field)
            {
                field.UpdateTower();
            }
        }
    }

    private Move extrapolateMove(Pos a, Pos b)
    {
        var moves = board.possMoves();
        foreach (var move in moves)
        {
            if (move[0].Equals(a) && move.lastPos().Equals(b))
            {
                return move;
            }
        }
        return null;
    }

    public void Select(GameObject tower)
    {
        if (selected)
        {
            selected.renderer.material.color = lastColor;
        }
        selected = tower.transform.GetChild(tower.transform.childCount - 1).gameObject;
        lastColor = selected.renderer.material.color;
        selected.renderer.material.color = Color.cyan;
    }

    public IEnumerator Move(Move move)
    {
        foreach (var field in transform.GetComponentsInChildren<WhiteField>())
        {
            if (field.IndexX == move[0].Col.Col && field.IndexY == move[0].Row.Row)
            {
                selected = field.transform.GetChild(0).gameObject;
            }
        }

        Vector3 from = selected.transform.position;
        Vector3 to = new Vector3(move.lastPos().Col.Col + .5f, from.y, move.lastPos().Row.Row + .5f);
        for (int i = 0; i < 10; ++i)
        {
            selected.transform.position = Vector3.Lerp(from, to, i / 10f);
            yield return null;
        }
        UpdateAllTowers();
        StartCoroutine(ChangeCameraAngle());
    }

    public IEnumerator ChangeCameraAngle()
    {
        bool isPvP = GameLoader.GameMode.ToLower() != "ai";
        if (isPvP)
        {
            int deg = 5;
            float amp = .2f;
            for (int i = 0; i < 180; i += deg)
            {
                Camera.main.transform.RotateAround(collider.bounds.center, Vector3.up, deg);
                Vector3 delta = new Vector3(0, Mathf.Cos(i * Mathf.Deg2Rad) * amp, 0);
                Camera.main.transform.position = Camera.main.transform.position + delta;
                yield return null;
            }
        }
        else
        {
            var moves = board.possMoves();
            if (moves.Count > 0)
            {
                if (board.Turn == Colour.White)
                {
                    var ratedMoves = new Dictionary<Move, ArtificialPlayer.Result>(moves.Count);
                    foreach (var move in moves)
                    {
                        ratedMoves.Add(move, ai.MinimaxAsync(board.doMove(move), 5, true));
                    }
                    bool hasWaited = false;
                    foreach (var entry in ratedMoves.Keys)
                    {
                        while (!ratedMoves[entry].Finished)
                        {
                            hasWaited = true;
                            // SUSPEND COROUTINE UNTIL ALL MOVES HAVE BEEN RATED
                            yield return null;
                        }
                    }
                    if (!hasWaited)
                    {
                        // WAIT ONE FRAME TO AWAIT PENDING DESTROYS
                        yield return null;
                    }
                    var highest = ratedMoves.OrderBy(keyValue => keyValue.Value.Value.Value).First().Key;
                    board = board.doMove(highest);
                    StartCoroutine(Move(highest));
                }
                else
                {
                    var move = moves.ToList()[Random.Range(0, moves.Count - 1)];
                    board = board.doMove(move);
                    // WAIT ONE FRAME TO AWAIT PENDING DESTROYS
                    yield return null;
                    StartCoroutine(Move(move));
                }
            }
            else
            {
                EndGUI.SetActive(true);
                IngameGUI.SetActive(false);
                Winner.text = board.Turn == Colour.Black ? "WHITE" : "BLACK";
                StartCoroutine(WinnerRotation());
            }
        }
    }

    IEnumerator WinnerRotation()
    {
        float angle = 0;
        int stepSize = 2;
        float startY = Camera.main.transform.position.y;
        while (true)
        {
            var delta = Time.deltaTime;
            Camera.main.transform.RotateAround(collider.bounds.center, Vector3.up, stepSize * delta * 75);
            Vector3 position = Camera.main.transform.position;
            position.y = startY + Mathf.Sin(angle * Mathf.Deg2Rad);
            Camera.main.transform.position = position;
            angle += stepSize * delta * 75;
            yield return null;
        }
    }

    public GameObject createTower(Tower tower)
    {
        GameObject towerObject = new GameObject("Tower");
        for (int i = tower.Count - 1; i >= 0; --i)
        {
            var counter = createCounter(tower.Get(i));
            counter.transform.parent = towerObject.transform;
            counter.transform.localPosition = new Vector3(0, (tower.Count - i - 1) * .2f, 0);
        }
        return towerObject;
    }

    public GameObject createCounter(Counter counter)
    {
        GameObject counterObject;
        if (counter.value == Value.Officer)
        {
            counterObject = Instantiate(OfficerPrefab) as GameObject;
        }
        else
        {
            counterObject = Instantiate(SoldierPrefab) as GameObject;
        }
        if (counter.color == Colour.White)
        {
            counterObject.renderer.material = WhiteMaterial;
        }
        else
        {
            counterObject.renderer.material = BlackMaterial;
        }
        return counterObject;
    }
}