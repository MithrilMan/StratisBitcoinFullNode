namespace Stratis.Features.Wallet.Repository.Extensions
{
    /// <summary>
    /// Extensions used to throw if a specific entity is null
    /// </summary>
    public static class WalletGuardExtensions
    {

        /// <summary>
        /// Throws if <paramref name="entity" /> is null.
        /// </summary>
        /// <typeparam name="TEntity">The type of the entity.</typeparam>
        /// <param name="entity">The entity to check.</param>
        /// <param name="message">The exception message.</param>
        /// <returns>
        /// The passed wallet
        /// </returns>
        /// <exception cref="Stratis.Features.Wallet.WalletNotFoundException">The passed wallet is null.</exception>
        /// <exception cref="HdAddressNotFoundException">The passed address is null.</exception>
        /// <exception cref="Stratis.Features.Wallet.WalletException">The passed object is null.</exception>
        public static TEntity ThrowIfNull<TEntity>(this TEntity entity, string message = null)
        {
            if (entity == null)
            {
                switch (entity)
                {
                    case IWallet _:
                        throw new WalletNotFoundException(message ?? "Wallet not found.");
                    case HdAddress _:
                        throw new HdAddressNotFoundException(message ?? "HdAddress not found.");
                    case HdAccount _:
                        throw new HdAccountNotFoundException(message ?? "HdAddress not found.");
                    default:
                        throw new WalletException(message ?? "Object is null");
                }
            }

            return entity;
        }
    }
}
