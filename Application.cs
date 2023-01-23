using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sitecore.XConnect;
using Sitecore.XConnect.Client;
using Sitecore.XConnect.Client.WebApi;
using Sitecore.XConnect.Collection.Model;
using Sitecore.XConnect.Schema;
using Sitecore.Xdb.Common.Web;

namespace DeleteContact
{
    class Application
    {
        private const int TimeoutIntervalMinutes = 15;
        private readonly IConfiguration _config;
        private readonly XdbModel _xDbModel = null;
        private readonly IServiceProvider _services = null;
        private XConnectClientConfiguration _xConnectClientConfiguration = null;
        private readonly Guid _channelId = new Guid("{ED10766E-C012-47F0-9DA4-7DD223F9EC73}");
        private int _contactCount = 3;
        private int _interactionCount = 10;
        public Application(IConfiguration config)
        {
            _config = config;
            _xDbModel = Sitecore.XConnect.Collection.Model.CollectionModel.Model;
            _services = CreateServices();
        }
        public async Task RunAsync()
        {
            System.Console.WriteLine("Delete Contact Demo");
            var option = (char)0;
            while (option != 'q')
            {
                PrintOptions();
                option = System.Console.ReadKey().KeyChar;
                System.Console.WriteLine();

                try
                {
                    switch (option)
                    {
                        case '1':
                            await SearchAllContactsAsync();
                            break;
                        case '2':
                            await ImportInteractionsAsync();
                            break;
                        case '3':
                            await SearchSingleContactAsync();
                            break;
                            
                        case 'q':
                            return;

                        default:
                            System.Console.WriteLine("Unknown option");
                            break;
                    }
                }
                catch (Exception e)
                {
                    var color = System.Console.ForegroundColor;
                    System.Console.ForegroundColor = ConsoleColor.Red;
                    System.Console.WriteLine(e);
                    System.Console.ForegroundColor = color;
                }
            }
        }

        private void PrintOptions()
        {
            System.Console.WriteLine();
            System.Console.WriteLine("Options:");
            System.Console.WriteLine("1.    Search for all contacts");
            System.Console.WriteLine($"2.    Import {_contactCount} contacts with {_interactionCount} interaction");
            System.Console.WriteLine("3.    Search for single contact");
            System.Console.WriteLine("q.    Quit");
            System.Console.WriteLine();
        }

        private async Task ImportInteractionsAsync()
        {

            System.Console.WriteLine($"Importing {_contactCount} contacts with {_interactionCount} interactions...");

         
            var xConnectClient = await CreateXConnectClient();

            var contacts = new List<Contact>();

            for (var i = 0; i < _contactCount; i++)
            {
                var contactId = new ContactIdentifier("music", Guid.NewGuid().ToString(), ContactIdentifierType.Known);
                var contact = new Contact(contactId);
                contacts.Add(contact);

                var definitionId = new Guid("{9326CB1E-CEC8-48F2-9A3E-91C7DBB2166C}");

                xConnectClient.AddContact(contact);
                for (var j = 0; j < _interactionCount; j++)
                {
                    var demoEvent = new Event(definitionId, DateTime.Now);
                    var interaction = new Interaction(contact, InteractionInitiator.Contact, _channelId, "user agent 1.0");
                    interaction.Events.Add(demoEvent);
                    xConnectClient.AddInteraction(interaction);
                }
                
                
            }

            await xConnectClient.SubmitAsync();

            foreach (var contact in contacts)
            {
                System.Console.WriteLine($"Created contact {contact.Id}");
            }
        }

        private async Task SearchAllContactsAsync()
        {
            var xConnectClient = await CreateXConnectClient();

            var query = xConnectClient.Contacts.Where(contact =>
                contact.Interactions.Any(interaction =>
                    interaction.Events.OfType<Event>().Any() 
                    && interaction.EndDateTime > DateTime.UtcNow.AddMinutes(-TimeoutIntervalMinutes)
                )
            );

            var expandOptions = new ContactExpandOptions
            {
                Interactions = new RelatedInteractionsExpandOptions()
            };

            query = query.WithExpandOptions(expandOptions);

            var batchEnumerator = await query.GetBatchEnumerator();
            System.Console.WriteLine($"Found {batchEnumerator.TotalCount} contacts");

            while (await batchEnumerator.MoveNext())
            {
                foreach (var contact in batchEnumerator.Current)
                {
                    System.Console.WriteLine("====================================");
                    System.Console.WriteLine($"Contact ID {contact.Id} has {contact.Interactions.Count()} interactions");
                }
            }
        }
        private async Task SearchSingleContactAsync()
        {
            string contactId;
            Console.Write("Enter Contact Id: ");
            contactId = Console.ReadLine();
            Guid? contactAsGuid = new Guid(contactId);
            var xConnectClient = await CreateXConnectClient();


            var expandOptions = new ContactExpandOptions(EmailAddressList.DefaultFacetKey)
            {
                Interactions = new RelatedInteractionsExpandOptions(IpInfo.DefaultFacetKey)
            };
            var reference = new Sitecore.XConnect.ContactReference(Guid.Parse(contactId));

            Task<Sitecore.XConnect.Contact> contactTask = xConnectClient.GetAsync<Sitecore.XConnect.Contact>(reference, expandOptions);

            Contact contact = await contactTask;


            System.Console.WriteLine("====================================");
            System.Console.WriteLine($"Contact ID {contact.Id} has {contact.Interactions.Count()} interactions");

            if (contact.IsKnown)
            {
                System.Console.WriteLine($"Contact is KNOWN");
            }
            else
            {
                System.Console.WriteLine($"Contact is not known");
            }

            foreach (var interaction in contact.Interactions.Take(5))
            {   
                var ipInfoFacet = interaction.GetFacet<IpInfo>(IpInfo.DefaultFacetKey);
                if (ipInfoFacet != null)
                {
                    System.Console.WriteLine($"IP Address is for interaction is {ipInfoFacet.IpAddress}");
                }
                else
                {
                    System.Console.WriteLine($"IP Address is for interaction is null");
                }
                var emailFacet = contact.GetFacet<EmailAddressList>(EmailAddressList.DefaultFacetKey);
                if (emailFacet != null)
                {
                    System.Console.WriteLine($"Email Address is {emailFacet.PreferredEmail.SmtpAddress}");
                }
                else
                {
                    System.Console.WriteLine($"Email Address is null");
                }
            }

            var option = (char)0;
            Console.Write("Press 1 to delete or any other key to cancel");
            System.Console.WriteLine();
            option = System.Console.ReadKey().KeyChar;
            try
            {
                switch (option)
                {
                    case '1':
                        Console.Write("Attempting to delete contact and all interactions");

                        await deleteContact(contact);
                        Console.Write("Finished deleting contact and all interactions");

                        break;
                    default:
                        break;
                }
            }
            catch (Exception e)
            {
                var color = System.Console.ForegroundColor;
                System.Console.ForegroundColor = ConsoleColor.Red;
                System.Console.WriteLine(e);
                System.Console.ForegroundColor = color;
            }
        }

        private async Task deleteContact(Contact contact)
        {
            using (XConnectClient xConnectClient = await CreateXConnectClient()) {
                xConnectClient.DeleteContact(contact);
                await xConnectClient.SubmitAsync();
            }
            System.Console.WriteLine();
            var color = System.Console.ForegroundColor;
            System.Console.ForegroundColor = ConsoleColor.Red;
            System.Console.WriteLine("Contact has been deleted");
            System.Console.ForegroundColor = color;
            System.Console.WriteLine();
        }

        private IServiceProvider CreateServices()
        {
            var serviceCollection = new ServiceCollection();

            // options
            serviceCollection.AddOptions();

            // logging
            serviceCollection.AddLogging();

            return serviceCollection.BuildServiceProvider();
        }
        private async Task<XConnectClient> CreateXConnectClient()
        {
            if (_xConnectClientConfiguration == null)
            {
                _xConnectClientConfiguration = await CreateXConnectClientConfiguration();
            }

            return new XConnectClient(_xConnectClientConfiguration);
        }

        private async Task<XConnectClientConfiguration> CreateXConnectClientConfiguration()
        {
            System.Console.WriteLine("Initializing xConnect configuration...");

            var uri = new Uri(_config.GetValue<string>("xconnect:uri"));
            var certificateSection = _config.GetSection("xconnect:certificate");
            var handlerModifiers = new List<IHttpClientHandlerModifier>();

            if (certificateSection.GetChildren().Any())
            {               
                var certificateModifier = new CertificateHttpClientHandlerModifier(certificateSection);
                handlerModifiers.Add(certificateModifier);
            }

            List<IHttpClientModifier> clientModifiers = new List<IHttpClientModifier>();
            var timeoutClientModifier = new TimeoutHttpClientModifier(new TimeSpan(6, 0, 0));
            clientModifiers.Add(timeoutClientModifier);

            var xConnectConfigurationClient = new ConfigurationWebApiClient(new Uri(uri + "configuration"), clientModifiers, handlerModifiers);
            var xConnectCollectionClient = new CollectionWebApiClient(new Uri(uri + "odata"), clientModifiers, handlerModifiers);
            var xConnectSearchClient = new SearchWebApiClient(new Uri(uri + "odata"), clientModifiers, handlerModifiers);

            var xConnectClientConfig = new XConnectClientConfiguration(_xDbModel, xConnectCollectionClient, xConnectSearchClient, xConnectConfigurationClient);
            await xConnectClientConfig.InitializeAsync();

            System.Console.WriteLine("xConnect configuration has been initialized");
            return xConnectClientConfig;
        }
    }
}
