FROM quay.io/keycloak/keycloak:26.4

# Copy the realm configuration
COPY MealPlanner.AppHost/Realms/mealplanner-realm.json /opt/keycloak/data/import/realm.json

# Build the image to optimize startup
RUN /opt/keycloak/bin/kc.sh build

# Start Keycloak with realm import enabled
ENTRYPOINT ["/opt/keycloak/bin/kc.sh"]
CMD ["start", "--import-realm", "--optimized"]
