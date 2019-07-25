# Wallet Discussion and Use Cases



## Introduction

Different needs needs different wallet implementations.

It's not viable to implement a *<u>one fits all</u>* solution, so we need a feature that act as a foundation to allow us to extend or create a new wallet, preserving a set of mandatory feature that a <u>*wallet implementation*</u> has to expose.

Current Wallet feature (`Stratis.Bitcoin.Features.Wallet`) doesn't allow us to extend its implementation and every other feature based on current wallet implementation, is tightly coupled with the `Wallet` and `WalletManager` classes, that exposes too many internal details.

Our first goal is to create Interfaces to be used outside, instead of reference current implementations, so for example would be possible to use the Mining feature with whatever wallet implementation the user prefer to mine.

Currently I've undertaken the task and I've created a repository I'm using to experiment with changes in the design: https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet
Whenever I'll refer to any code of the new wallet feature, that's the starting point to take a look at.

The new feature is actually named `Stratis.Features.Wallet` (I just removed the .Bitcoin part from the previous wallet feature project name).
Here the project: https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/src/Stratis.Features.Wallet

Up to now my effort has been to try to reduce the surface of the needed changes, basically trying to preserve previous design where possible and I've encountered several problems because of the lack of interfaces and implementation tightly bound to the concept of the wallet as a single document: currently the wallet is kept all in memory and whenever a change is done, the wallet is flushed as a whole in Json format; this mean of course that the more the wallet increase in data, the more the fullnode will spend in serialization and the more memory is used to keep all wallet information at hand.



## Current Implementation

Let's start by describing what we have right now.
Our wallet implementation, from a model/entity perspective, is an [HD Wallet](https://en.bitcoin.it/wiki/Deterministic_wallet).

Implementation details are:

1. Persistence layer is [JSON](https://www.json.org/) file.
2. Whole wallet, when loaded from the FullNode, is kept in memory (RAM).
3. Every query on wallet transaction is done on the fly using in-memory data, traversing the wallet model, without using any lookup structure to speedup queries.



## What to save from current implementation

The Wallet model can be preserved in term of entity design, except fixing some design issues like `AccountsRoot` property that's actually a collection but we expect it to have only a single entry (or the wallet will throw exceptions).

Current hierarchy is

```
Wallet
 => AccountsRoot `AccountRoot[]` (array but as said is a single entry)
     => Accounts `HdAccount[]`
       => ExternalAddresses `HdAddress[]`
       => InternalAddresses `HdAddress[]
```

IWalletManager/WalletManager need to be refactored in order to not expose methods tight to the model they are based on (like Json wallet file)



## What's missing

Current implementation isn't scalable, we can't store everything in memory and it's not good to persist the whole document at every change, so we need an approach that lead to a DB design. Single document may still be useful for very simple scenarios and for portability or as a format to import/export single wallets between nodes.
So we need for sure scalability.

Another aspect is missing features: prior to being HD, wallets were just a bunch of containers of randomly generated addresses with a pool of addresses ready to be used (and of course transaction details related to these addresses).
These addresses were impossible to be recovered from a common seed, so there was the need to backup the wallet or the funds would have been impossible to recover in case of failure.

At the time the wallet had features to export (and import) single addresses private keys

Now with HD wallet there isn't that need anymore: with a single seed you can restore the whole wallet easier.
This however doesn't mean that we don't need such feature, so this should be source for further discussion: did we need to support the import and export of single address private keys?

For other wallet consideration, refers to [Kevin](#Special Guest: Kevin) notes.



## Current approach being implemented

The focus is on implementing a IWallet interface that requires the common stuff that ANY wallet implementation need to implement. At the same time the effort is put on implementing the same thing for IWalletManager/WalletManager.

To be able to handle both document-like wallets (e.g. json file) and Database wallet, I'm creating a BaseWalletManager that implements already the common stuff, and I plan then to have JsonWalletManager and DBWalletManager, to be able to pick the preferred one.

DBWalletManager would act as a base implementation, agnostic to the underneeth database engine.
The persistence layer will be in its own feature (e.g. Stratis.Features.Wallet.LiteDB for  litedb storage), this way would be really easy to switch the DB engine used.

There will be a IWalletRepository interface that will require all basic methods to fetch and put data.

Indexes on DB solutions are very important, so we should design the DB structure according to use-cases in order to handle things properly. We may even want to de-normalize some tables in order to have summaries like wallet balances available at low cost.

## Use Cases

### Average Joe

- Simple P2PKH sending, typically to only one recipient
- Automatic change address handling
  Automatic shuffling of outputs for privacy enhancement
- OP_RETURN transaction construction for cross-chain transfers

### Exchanges

- Usually P2PKH sending
- Can be to very large numbers of recipients
- Change address may be automatic or specified by exchange (cold storage)
  Therefore, the API to provide transaction construction needs to be carefully considered

- UTXO selection (exchanges)
  We may need to be able to configure coin selection 'strategies' that the wallet will use to select UTXOs. These could be:

  - Minimum fees
    choose large enough UTXO with simple spending conditions so that fee is lowest possible
  - Promote consolidation
    select the smallest possible UTXOs first without regard to the impact on fees
  - Balanced consolidation (some mixture of the above two)

  

Open Questions:

- how to handle Cirrus transfers?
- They may want to be able to build a transaction and sign it using an offline HSM, how would we accomodate that?
- Expose identical RPCs to Bitcoin Core? Or is the web API sufficient?
- Exchanges may use their own databases for transaction history, in which case the wallet could be optimized for UTXO management rather than maintaining full history





## Special Guest: Kevin

Here some notes from Kevin:

```
Multiple address type handling

Currently the wallet only supports the following address types:
- P2PK (pay to pubkey, not actually an address type)
- P2PKH (pay to pubkey hash)

Cold staking is an additional transaction type supported by the wallet I am not actually sure offhand whether this has its own address format.
I think it does not, and there are instead two P2PKH addresses associated with the hot and cold wallet respectively.
If that is the case then there is no additional implementation complexity to support cold staking from an address standpoint.

Of the two address types, P2PKH is by far the most common for transacting. P2PK is legacy and generally only used for mining.
It does not actually have an address format associated with it, and wallets/block explorers typically display the P2PKH address of the P2PK scriptPubKey.
This is incorrect but well entrenched in production deployments and therefore should not be changed.

Generally one refers to different types of scriptPubKeys rather than addresses, an address is just a more human-friendly construct.
The underlying wallet design only needs to know how to derive an address for a given
scriptPubKey, it does not necessarily need to store the address as a separate data item.

Going forwards we need to support an additional address (scriptPubKey) type:

- P2SH (pay to script hash), typically used for multisig and other advanced transaction types

The introduction of segwit support will result in further types being needed:

- P2WPKH (the segwit form of P2PKH)
- P2WSH (the segwit form of P2SH)
- P2WPKH wrapped in P2SH (this is needed so that legacy wallets are still able to send funds to segwit wallets)
- P2WSH wrapped in P2SH (this is needed so that legacy wallets are still able to send funds to segwit wallets)

There are other types that are not generally needed at a wallet level:

- bare multisig (no address format)

The difficulty these aforementioned types introduce is that we have to scan every transaction input and output of every incoming block in order to determine whether we are receiving funds into an address/scriptPubKey or paying them out.
Therefore, the performance of this scanning process is critical for large wallets.

My suggestion is that we have an index of each of the types we expect to be used, so for example an incoming tranaction can be compared against:

- P2PKH index
- P2PK index
- P2SH index
- Segwit indices

These could be selectively enabled/disabled depending on the user's requirements, e.g if segwit is not used.
For very large numbers of addresses we could perhaps investigate esoteric data structures like bloom filters to enable very rapid lookup with fallback to the underlying data store only where needed (depending on false positives or false negatives).

A further difficulty is that additional data needs to be stored in order to allow particular UTXOs to be spent.
For a P2SH address, it is vital that the corresponding script (called the redeemScript) is stored.
It may make sense to introduce a SpendingRequirements model for complex transaction types that require such data.
Each wallet sub-variant could expect to see different fields in this model.

We could also only support very limited P2SH transactions (e.g. purely the wrapped segwit version of wallet addresses) in order to keep initial implementation effort to a minimum.
```



## Open Issues

##### Chose a database engine

Things to consider.

Did we want to use only engines that:

- have a native C# implementation

- are embedded (no need to run external installers)
- are the same used by bitcoin core: LevelDb, Barkeley DB (bitcoin core wallet uses Berkeley DB)
- allow (multiple) indexing

###### Known Databases:

| Database Engine                                   | DB Type         | Pros                                 | Cons                                                    |
| ------------------------------------------------- | --------------- | ------------------------------------ | ------------------------------------------------------- |
| [DBreeze](https://github.com/hhblaze/DBreeze)     | Key-Value       | Embedded<br />Native<br />ACID       | One Man Band<br />Logical deletes                       |
| [LiteDb](https://www.litedb.org/)                 | Key-Value       | Embedded<br />Native<br />~~ACID~~   | Poor documentation<br />Not ACID (missing transactions) |
| [LevelDb](https://github.com/google/leveldb)      | Key-Value       | Embeddable<br />Used by bitcoin      | Doesn't officially support C#<br />Not ACID             |
| [FASTER](https://microsoft.github.io/FASTER/)     | Key-Value       | Embeddable<br />Made by MS           | flagged as Research, not sure if production ready       |
| [SQLite](https://db-engines.com/en/system/SQLite) | Relational DBMS | ACID<br />Well Known<br />Embeddable | Not Native                                              |
| RockDb                                            |                 |                                      |                                                         |



##### RPC Throughput

We need to test RPC calls throughput , related to StratisX.



##### Non HD addresses

Should we support import and export of private keys in our wallet implementation?



---

Repository reference for implementations: https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/

old feature implementation: [Stratis.Bitcoin.Features.Wallet](https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/src/Stratis.Bitcoin.Features.Wallet)
new one: [Stratis.Features.Wallet](https://github.com/MithrilMan/StratisBitcoinFullNode/tree/research/wallet/src/Stratis.Features.Wallet)