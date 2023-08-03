#include <iostream>
#include "crow_all.h"

int main()
{
    crow::SimpleApp app;

    CROW_ROUTE(app, "/api")([](){
        return "Hello world";
    });

    app.port(18080).run();
}
