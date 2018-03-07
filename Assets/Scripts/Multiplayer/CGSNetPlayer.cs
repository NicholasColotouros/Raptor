﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;

public class CGSNetPlayer : NetworkBehaviour
{
    public const string ShareDeckRequest = "Would you like to share the host's deck?";

    [SyncVar]
    public float Points;
    public List<Card> CurrentDeck { get; } = new List<Card>();

    public override void OnStartLocalPlayer()
    {
        base.OnStartLocalPlayer();
        CGSNetManager.Instance.LocalPlayer = this;
        Points = CardGameManager.Current.GameStartPointsCount;
        if (!isServer)
            RequestCardGame();
    }

    public void RequestCardGame()
    {
        CmdSelectCardGame();
    }

    [Command]
    public void CmdSelectCardGame()
    {
        TargetSelectCardGame(connectionToClient, CardGameManager.Current.Name, CardGameManager.Current.AutoUpdateUrl);
    }

    [TargetRpc]
    public void TargetSelectCardGame(NetworkConnection target, string gameName, string gameUrl)
    {
        CardGameManager.Instance.SelectCardGame(gameName, gameUrl);
        StartCoroutine(WaitToRequestDeck());
    }

    public IEnumerator WaitToRequestDeck()
    {
        while(CardGameManager.Current.IsDownloading)
            yield return null;
        CardGameManager.Instance.Messenger.Ask(ShareDeckRequest, CGSNetManager.Instance.playController.ShowDeckMenu, RequestDeck);
    }

    public void RequestDeck()
    {
        CmdShareDeck();
    }

    [Command]
    public void CmdShareDeck()
    {
        List<Card> deckCards = CGSNetManager.Instance.LocalPlayer.CurrentDeck;
        TargetShareDeck(connectionToClient, deckCards.Select(card => card.Id).ToArray());
    }

    [TargetRpc]
    public void TargetShareDeck(NetworkConnection target, string[] cardIds)
    {
        List<Card> cards = cardIds.Select(cardId => CardGameManager.Current.Cards[cardId]).ToList();
        CGSNetManager.Instance.playController.LoadDeck(cards);
    }

    public void MoveCardToServer(CardStack cardStack, CardModel cardModel)
    {
        cardModel.transform.SetParent(cardStack.transform);
        cardModel.LocalPosition = cardModel.transform.localPosition;
        cardModel.Rotation = cardModel.transform.rotation;
        CmdSpawnCard(cardModel.Id, cardModel.LocalPosition, cardModel.Rotation, cardModel.IsFacedown);
        Destroy(cardModel.gameObject);
    }

    [Command]
    public void CmdSpawnCard(string cardId, Vector3 localPosition, Quaternion rotation, bool isFacedown)
    {
        PlayMode controller = CGSNetManager.Instance.playController;
        GameObject newCard = Instantiate(CGSNetManager.Instance.cardModelPrefab, controller.playAreaContent);
        CardModel cardModel = newCard.GetComponent<CardModel>();
        cardModel.Value = CardGameManager.Current.Cards[cardId];
        cardModel.transform.localPosition = localPosition;
        cardModel.LocalPosition = localPosition;
        cardModel.transform.rotation = rotation;
        cardModel.Rotation = rotation;
        cardModel.IsFacedown = isFacedown;
        controller.SetPlayActions(controller.playAreaContent.GetComponent<CardStack>(), cardModel);
        NetworkServer.SpawnWithClientAuthority(newCard, gameObject);
        cardModel.RpcHideHighlight();
    }
}
