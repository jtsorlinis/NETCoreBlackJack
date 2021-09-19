using System;
using System.Collections.Generic;
using System.Linq;

namespace CSharpBlackJack
{
  internal class Table
  {
    private readonly Dealer _dealer = new();
    private readonly int _minCards;
    private readonly int _numOfDecks;
    private readonly char[] _stratHard = Strategies.Array2dToMap(Strategies.StratHard);
    private readonly char[] _stratSoft = Strategies.Array2dToMap(Strategies.StratSoft);
    private readonly char[] _stratSplit = Strategies.Array2dToMap(Strategies.StratSplit);
    private readonly bool _verbose;
    public readonly int mBetSize;
    public readonly CardPile mCardPile;
    public readonly List<Player> mPlayers;
    private int _currentPlayer;
    private int _runningCount;
    private int _trueCount;
    public float mCasinoEarnings = 0;

    public Table(int numPlayers, int numDecks, int betSize, int minCards, bool verbose)
    {
      mCardPile = new CardPile(numDecks);
      _verbose = verbose;
      mBetSize = betSize;
      _numOfDecks = numDecks;
      _minCards = minCards;
      mPlayers = new(numPlayers * 3);
      for (var i = 0; i < numPlayers; i++) mPlayers.Add(new Player(this));
    }

    private void DealRound()
    {
      for (var i = 0; i < mPlayers.Count; i++)
      {
        Deal();
        _currentPlayer++;
      }
      _currentPlayer = 0;
    }

    private void EvaluateAll()
    {
      for (var i = 0; i < mPlayers.Count; i++)
      {
        mPlayers[i].Evaluate();
      }
    }

    private void Deal()
    {
      var card = mCardPile.mCards[^1];
      mPlayers[_currentPlayer].mHand.Add(card);
      _runningCount += card.mCount;
      mCardPile.mCards.RemoveAt(mCardPile.mCards.Count - 1);
    }

    private void PreDeal()
    {
      for (var i = 0; i < mPlayers.Count; i++) SelectBet(mPlayers[i]);
    }

    private void SelectBet(Player player)
    {
      if (_trueCount >= 2) player.mInitialBet = mBetSize * (_trueCount - 1);
    }

    private void DealDealer(bool faceDown = false)
    {
      var card = mCardPile.mCards[^1];
      mCardPile.mCards.RemoveAt(mCardPile.mCards.Count - 1);
      card.mFaceDown = faceDown;
      _dealer.mHand.Add(card);
      if (!faceDown) _runningCount += card.mCount;
    }

    public void StartRound()
    {
      Clear();
      UpdateCount();
      if (_verbose)
      {
        Console.WriteLine(mCardPile.mCards.Count + " cards left");
        Console.WriteLine("Running count is: " + _runningCount + "\tTrue count is: " + _trueCount);
      }

      GetNewCards();
      PreDeal();
      DealRound();
      DealDealer();
      DealRound();
      DealDealer(true);
      EvaluateAll();
      _currentPlayer = 0;
      if (CheckDealerNatural())
      {
        FinishRound();
      }
      else
      {
        CheckPlayerNatural();
        if (_verbose) Print();
        AutoPlay();
      }
    }

    private void GetNewCards()
    {
      if (mCardPile.mCards.Count >= _minCards) return;
      mCardPile.Refresh();
      mCardPile.Shuffle();
      _trueCount = 0;
      _runningCount = 0;
      if (_verbose)
        Console.WriteLine("Got " + _numOfDecks + " new decks as number of cards left is below " +
                          _minCards);
    }

    public void Clear()
    {
      for (var i = mPlayers.Count - 1; i >= 0; i--)
      {
        if (mPlayers[i].mSplitFrom != null)
        {
          mPlayers[i - 1].mEarnings += mPlayers[i].mEarnings;
          mPlayers.RemoveAt(i);
        }
        else
        {
          mPlayers[i].ResetHand();
        }
      }

      _dealer.ResetHand();
      _currentPlayer = 0;
    }

    private void UpdateCount()
    {
      if (mCardPile.mCards.Count > 51) _trueCount = _runningCount / (mCardPile.mCards.Count / 52);
    }

    private void Hit()
    {
      Deal();
      mPlayers[_currentPlayer].Evaluate();
      if (_verbose) Console.WriteLine("Player " + mPlayers[_currentPlayer].mPlayerNum + " hits");
    }

    private void Stand()
    {
      if (_verbose && mPlayers[_currentPlayer].mValue <= 21)
      {
        Console.WriteLine("Player " + mPlayers[_currentPlayer].mPlayerNum + " stands");
        Print();
      }

      mPlayers[_currentPlayer].mIsDone = true;
    }

    private void Split()
    {
      var splitPlayer = new Player(this, mPlayers[_currentPlayer]);
      mPlayers[_currentPlayer].mHand.RemoveAt(mPlayers[_currentPlayer].mHand.Count - 1);
      mPlayers.Insert(_currentPlayer + 1, splitPlayer);
      mPlayers[_currentPlayer].Evaluate();
      mPlayers[_currentPlayer + 1].Evaluate();
      if (_verbose) Console.WriteLine("Player " + mPlayers[_currentPlayer].mPlayerNum + " splits");
    }

    private void SplitAces()
    {
      if (_verbose) Console.WriteLine("Player " + mPlayers[_currentPlayer].mPlayerNum + " splits Aces");
      var splitPlayer = new Player(this, mPlayers[_currentPlayer]);
      mPlayers[_currentPlayer].mHand.RemoveAt(mPlayers[_currentPlayer].mHand.Count - 1);
      mPlayers.Insert(_currentPlayer + 1, splitPlayer);
      Deal();
      mPlayers[_currentPlayer].Evaluate();
      Stand();
      _currentPlayer++;
      Deal();
      mPlayers[_currentPlayer].Evaluate();
      Stand();
      if (_verbose) Print();
    }

    private void DoubleBet()
    {
      if (mPlayers[_currentPlayer].mBetMult < 1.1 && mPlayers[_currentPlayer].mHand.Count == 2)
      {
        mPlayers[_currentPlayer].DoubleBet();
        if (_verbose) Console.WriteLine("Player " + mPlayers[_currentPlayer].mPlayerNum + " doubles");
        Hit();
        Stand();
      }
      else
      {
        Hit();
      }
    }

    private void AutoPlay()
    {
      while (!mPlayers[_currentPlayer].mIsDone)
      {
        // check if player just split
        if (mPlayers[_currentPlayer].mHand.Count == 1)
        {
          if (_verbose)
            Console.WriteLine("Player " + mPlayers[_currentPlayer].mPlayerNum +
                              " gets 2nd card after splitting");
          Deal();
          mPlayers[_currentPlayer].Evaluate();
        }

        if (mPlayers[_currentPlayer].mHand.Count < 5 && mPlayers[_currentPlayer].mValue < 21)
        {
          var splitCardVal = mPlayers[_currentPlayer].CanSplit();
          if (splitCardVal == 11)
            SplitAces();
          else if (splitCardVal != 0 && splitCardVal != 5 && splitCardVal != 10)
            Action(Strategies.GetAction(splitCardVal, _dealer.UpCard(), _stratSplit));
          else if (mPlayers[_currentPlayer].mIsSoft)
            Action(Strategies.GetAction(mPlayers[_currentPlayer].mValue, _dealer.UpCard(), _stratSoft));
          else
            Action(Strategies.GetAction(mPlayers[_currentPlayer].mValue, _dealer.UpCard(), _stratHard));
        }
        else
        {
          Stand();
        }
      }

      NextPlayer();
    }

    private void Action(char action)
    {
      switch (action)
      {
        case 'H':
          Hit();
          break;
        case 'S':
          Stand();
          break;
        case 'D':
          DoubleBet();
          break;
        case 'P':
          Split();
          break;
        default:
          Console.WriteLine("No action found");
          Environment.Exit(1);
          break;
      }
    }

    private void DealerPlay()
    {
      var allBusted = true;
      for (var i = 0; i < mPlayers.Count; i++)
        if (mPlayers[i].mValue < 22)
        {
          allBusted = false;
          break;
        }

      _dealer.mHand[1].mFaceDown = false;
      _runningCount += _dealer.mHand[1].mCount;
      _dealer.Evaluate();
      if (_verbose)
      {
        Console.WriteLine("Dealer's turn");
        Print();
      }

      if (allBusted)
      {
        if (_verbose) Console.WriteLine("Dealer automatically wins cause all players busted");
        FinishRound();
      }
      else
      {
        while (_dealer.mValue < 17 && _dealer.mHand.Count < 5)
        {
          DealDealer();
          _dealer.Evaluate();
          if (!_verbose) continue;
          Console.WriteLine("Dealer hits");
          Print();
        }

        FinishRound();
      }
    }

    private void NextPlayer()
    {
      if (++_currentPlayer < mPlayers.Count)
        AutoPlay();
      else
        DealerPlay();
    }

    private void CheckPlayerNatural()
    {
      for (var i = 0; i < mPlayers.Count; i++)
        if (mPlayers[i].mValue == 21 && mPlayers[i].mHand.Count == 2 && mPlayers[i].mSplitFrom == null)
          mPlayers[i].mHasNatural = true;
    }

    private bool CheckDealerNatural()
    {
      _dealer.Evaluate();
      if (_dealer.mValue != 21) return false;
      _dealer.mHand[1].mFaceDown = false;
      _runningCount += _dealer.mHand[1].mCount;
      if (!_verbose) return true;
      Print();
      Console.WriteLine("Dealer has a natural 21");

      return true;
    }

    public void CheckEarnings()
    {
      float check = 0;
      for (var i = 0; i < mPlayers.Count; i++) check += mPlayers[i].mEarnings;
      if (check + mCasinoEarnings != 0)
      {
        Console.WriteLine("Earnings don't match");
        Environment.Exit(1);
      }
    }

    private void FinishRound()
    {
      if (_verbose) Console.WriteLine("Scoring round");
      for (var i = 0; i < mPlayers.Count; i++)
      {
        if (mPlayers[i].mHasNatural)
        {
          mPlayers[i].Win(1.5f);
          if (_verbose)
            Console.WriteLine("Player " + mPlayers[i].mPlayerNum + " Wins " +
                              1.5 * mPlayers[i].mBetMult * mPlayers[i].mInitialBet + " with a natural 21");
        }
        else if (mPlayers[i].mValue > 21)
        {
          mPlayers[i].Lose();
          if (_verbose)
            Console.WriteLine("Player " + mPlayers[i].mPlayerNum + " Busts and Loses " +
                              mPlayers[i].mBetMult * mPlayers[i].mInitialBet);
        }
        else if (_dealer.mValue > 21 || mPlayers[i].mValue > _dealer.mValue)
        {
          mPlayers[i].Win();
          if (_verbose)
            Console.WriteLine("Player " + mPlayers[i].mPlayerNum + " Wins " +
                              mPlayers[i].mBetMult * mPlayers[i].mInitialBet);
        }
        else if (mPlayers[i].mValue == _dealer.mValue)
        {
          if (_verbose) Console.WriteLine("Player " + mPlayers[i].mPlayerNum + " Draws");
        }
        else
        {
          mPlayers[i].Lose();
          if (_verbose)
            Console.WriteLine("Player " + mPlayers[i].mPlayerNum + " Loses " +
                              mPlayers[i].mBetMult * mPlayers[i].mInitialBet);
        }
      }

      if (!_verbose) return;
      {
        foreach (var player in mPlayers.Where(player => player.mSplitFrom == null))
          Console.WriteLine("Player " + player.mPlayerNum + " Earnings: " + player.mEarnings);
        Console.WriteLine();
      }
    }

    private void Print()
    {
      foreach (var player in mPlayers) Console.WriteLine(player.Print());
      Console.WriteLine(_dealer.Print() + "\n");
    }
  }
}