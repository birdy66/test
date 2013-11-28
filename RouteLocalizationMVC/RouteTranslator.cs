﻿namespace RouteLocalizationMVC
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Reflection;
	using System.Text.RegularExpressions;
	using System.Web.Routing;
	using RouteLocalizationMVC.Extensions;
	using RouteLocalizationMVC.Setup;

	public class RouteTranslator
	{
		public RouteTranslator()
		{
			Configuration = new Configuration();
		}

		public RouteTranslator(Configuration configuration)
		{
			Configuration = configuration;
		}

		public string Action { get; set; }

		public ICollection<Type> ActionArguments { get; set; }

		public string AreaPrefix { get; set; }

		public Configuration Configuration { get; set; }

		public string Controller { get; set; }

		public string ControllerNamespace { get; set; }

		public string Culture { get; set; }

		public string NamedRoute { get; set; }

		public RouteCollection RouteCollection { get; set; }

		public string RoutePrefix { get; set; }

		public RouteTranslator AddTranslation(string url)
		{
			if (string.IsNullOrEmpty(NamedRoute))
			{
				return AddTranslation(url, Culture, Controller, Action, ControllerNamespace, ActionArguments);
			}
			else
			{
				return AddTranslationForNamedRoute(url, Culture, NamedRoute);
			}
		}

		public RouteTranslator AddTranslation(string url, string culture)
		{
			if (string.IsNullOrEmpty(NamedRoute))
			{
				return AddTranslation(url, culture, Controller, Action, ControllerNamespace, ActionArguments);
			}
			else
			{
				return AddTranslationForNamedRoute(url, Culture, NamedRoute);
			}
		}

		public RouteTranslator AddTranslation(string url, string culture, string action)
		{
			return AddTranslation(url, culture, Controller, action, ControllerNamespace, ActionArguments);
		}

		public RouteTranslator AddTranslation(string url, string culture, string controller, string action,
			string controllerNamespace, ICollection<Type> actionArguments)
		{
			if (string.IsNullOrEmpty(controller))
			{
				throw new ArgumentNullException("controller");
			}

			if (string.IsNullOrEmpty(action))
			{
				throw new ArgumentNullException("action");
			}

			Route route = RouteCollection.GetFirstUntranslatedRoute(culture, controller, action, controllerNamespace,
				actionArguments);

			if (route == null)
			{
				throw new InvalidOperationException(string.Format("No Route found for given Controller '{0}' and Action '{1}'.",
					controller, action));
			}

			return AddTranslation(url, culture, route);
		}

		public RouteTranslator AddTranslation(string url, string culture, Route route)
		{
			if (Configuration.ValidateCulture && !Configuration.AcceptedCultures.Contains(culture))
			{
				throw new InvalidOperationException(string.Format("AcceptedCultures does not contain culture '{0}'.", culture));
			}

			// Store index so we can replace on the same position
			int routeIndex = RouteCollection.IndexOf(route);

			TranslationRoute routeTranslationRoute;

			// Prepare RootRoute
			if (!(route is TranslationRoute))
			{
				// Check if the route we want to replace is a named route
				string routeName = RouteCollection.NamedMap()
					.Where(x => x.Value == route).Select(x => x.Key).SingleOrDefault();

				// Remove route
				RouteCollection.Remove(route);

				routeTranslationRoute = route.ToTranslationRoute();

				switch (Configuration.RootTranslationProcessing)
				{
					case RootTranslationProcessing.ApplyDefaultCultureToRoute:

						// Set original route as translated route with DefaultCulture
						routeTranslationRoute.Culture = Configuration.DefaultCulture;

						if (Configuration.AddCultureAsRoutePrefix)
						{
							routeTranslationRoute.Url = string.Format("{0}/{1}", routeTranslationRoute.Culture, routeTranslationRoute.Url);
						}

						break;

					case RootTranslationProcessing.ReplaceRouteByTranslatedRoute:

						// Replace Original Route by translated Route
						routeTranslationRoute = TranslateRoute(url, culture, routeTranslationRoute);
						break;

					case RootTranslationProcessing.None:

						// Just replacing with translated route (original route stays as "neutral" route without culture)
						break;
				}

				RouteCollection.Insert(routeIndex, routeTranslationRoute);

				// Named route exist, so add dictionary entry
				if (!string.IsNullOrEmpty(routeName))
				{
					RouteCollection.NamedMap().Add(routeName, routeTranslationRoute);
				}

				if (Configuration.RootTranslationProcessing == RootTranslationProcessing.ReplaceRouteByTranslatedRoute)
				{
					// No further processing needed
					return this;
				}
			}
			else
			{
				// We already have a translated route
				routeTranslationRoute = (TranslationRoute)route;
			}

			// Create translated route
			TranslationRoute translationRoute = TranslateRoute(url, culture, routeTranslationRoute);

			// Set TranslationRouteRoot on translated route
			translationRoute.TranslationRouteRoot = routeTranslationRoute;

			// Add translated route to TranslationRouteRoot
			routeTranslationRoute.TranslatedRoutes.Add(culture, translationRoute);

			// Insert after root
			RouteCollection.Insert(routeIndex + 1, translationRoute);

			return this;
		}

		public RouteTranslator AddTranslationForNamedRoute(string url, string culture, string namedRoute)
		{
			Route route = RouteCollection.GetUntranslatedNamedRoute(culture, namedRoute);

			if (route == null)
			{
				throw new InvalidOperationException(string.Format("No Route found for name'{0}'.", namedRoute));
			}

			return AddTranslation(url, culture, route);
		}

		public RouteTranslator ForAction(string action)
		{
			return ForAction(action, null);
		}

		public RouteTranslator ForAction(string action, Type[] actionArguments)
		{
			Action = action;
			ActionArguments = actionArguments;

			return this;
		}

		public RouteTranslator ForController(string controller, string controllerNamespace)
		{
			Controller = controller;
			ControllerNamespace = controllerNamespace;

			return this;
		}

		public RouteTranslator<T> ForController<T>()
		{
			Controller = Regex.Replace(typeof(T).Name, "Controller$", "");
			ControllerNamespace = typeof(T).Namespace;

			return ToGeneric<T>();
		}

		public RouteTranslator ForCulture(string culture)
		{
			Culture = culture;

			return this;
		}

		public RouteTranslator ForNamedRoute(string namedRoute)
		{
			NamedRoute = namedRoute;

			return this;
		}

		public RouteTranslator SetAreaPrefix(string areaPrefix)
		{
			AreaPrefix = areaPrefix;

			return this;
		}

		public RouteTranslator SetRoutePrefix(string routePrefix)
		{
			RoutePrefix = routePrefix;

			return this;
		}

		protected RouteTranslator<T> ToGeneric<T>()
		{
			return new RouteTranslator<T>
			{
				Action = Action,
				ActionArguments = ActionArguments,
				AreaPrefix = AreaPrefix,
				Configuration = Configuration,
				Controller = Controller,
				ControllerNamespace = ControllerNamespace,
				Culture = Culture,
				NamedRoute = NamedRoute,
				RouteCollection = RouteCollection,
				RoutePrefix = RoutePrefix
			};
		}

		protected TranslationRoute TranslateRoute(string url, string culture, TranslationRoute routeTranslationRoute)
		{
			TranslationRoute translationRoute = routeTranslationRoute.ToTranslationRoute();
			translationRoute.Culture = culture;

			// Apply Route and Area Prefix
			url = string.IsNullOrEmpty(RoutePrefix) ? url : string.Format("{0}/{1}", RoutePrefix, url);
			url = string.IsNullOrEmpty(AreaPrefix) ? url : string.Format("{0}/{1}", AreaPrefix, url);
			url = !Configuration.AddCultureAsRoutePrefix ? url : string.Format("{0}/{1}", translationRoute.Culture, url);

			translationRoute.Url = url;

			// Validate and check if translation has identical placeholders
			if (Configuration.ValidateURL)
			{
				MatchCollection originalMatches = Regex.Matches(routeTranslationRoute.Url, "{.*?}");
				MatchCollection translationMatches = Regex.Matches(translationRoute.Url, "{.*?}");

				if (originalMatches.Count != translationMatches.Count)
				{
					throw new InvalidOperationException(
						string.Format(
							"Translation Route '{0}' contains different number of {{ }} placeholders than original Route '{1}'." +
								"Set Configuration.ValidateURL to false, if you want to skip validation.", translationRoute.Url,
							routeTranslationRoute.Url));
				}

				for (int i = 0; i < originalMatches.Count; i++)
				{
					if (originalMatches[i].Value != translationMatches[i].Value)
					{
						throw new InvalidOperationException(
							string.Format(
								"Translation Route '{0}' contains different {{ }} placeholders than original Route '{1}'." +
									"Set Configuration.ValidateURL to false, if you want to skip validation.", translationRoute.Url,
								routeTranslationRoute.Url));
					}
				}
			}

			return translationRoute;
		}
	}
}