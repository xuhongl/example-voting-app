package worker;

import redis.clients.jedis.Jedis;
import redis.clients.jedis.exceptions.JedisConnectionException;
import java.sql.*;
import org.json.JSONObject;

class Worker {
  public static void main(String[] args) {    
    String redisHostname = ((redisHostname = System.getenv("ENV_VAR_REDIS_HOST")) != null) ? redisHostname : "redis";
    String redisPort = ((redisPort = System.getenv("ENV_VAR_REDIS_PORT")) != null) ? redisPort : "6379";
    String redisPassword = ((redisPassword = System.getenv("ENV_VAR_REDIS_PASSWORD")) != null) ? redisPassword : "redis_password";    
    String pgHost = ((pgHost = System.getenv("ENV_VAR_POSTGRES_HOST")) != null) ? pgHost : "db";
    String pgPort = ((pgPort = System.getenv("ENV_VAR_POSTGRES_PORT")) != null) ? pgPort : "5432";
    String pgDatabase = ((pgDatabase = System.getenv("ENV_VAR_POSTGRES_DATABASE")) != null) ? pgDatabase : "postgres";
    String pgUser = ((pgUser = System.getenv("ENV_VAR_POSTGRES_USER")) != null) ? pgUser : "postgres_user";
    String pgPassword = ((pgPassword = System.getenv("ENV_VAR_POSTGRES_PASSWORD")) != null) ? pgPassword : "postgres_password";
    // Syntax: jdbc:postgresql://host:port/database
    String connectionString = "jdbc:postgresql://" + pgHost + ":" + pgPort + "/" + pgDatabase;
    System.err.println("Using connection string: " + connectionString);
    
    try {
      Jedis redis = connectToRedis(redisHostname, redisPassword);
      Connection dbConn = connectToDB(connectionString, pgUser, pgPassword);

      System.err.println("Watching vote queue");

      while (true) {
        String voteJSON = redis.blpop(0, "votes").get(1);
        JSONObject voteData = new JSONObject(voteJSON);
        String voterID = voteData.getString("voter_id");
        String vote = voteData.getString("vote");

        System.err.printf("Processing vote for '%s' by '%s'\n", vote, voterID);
        updateVote(dbConn, voterID, vote);
      }
    } catch (SQLException e) {
      e.printStackTrace();
      System.exit(1);
    }
  }

  static void updateVote(Connection dbConn, String voterID, String vote) throws SQLException {
    PreparedStatement insert = dbConn.prepareStatement(
      "INSERT INTO votes (id, vote) VALUES (?, ?)");
    insert.setString(1, voterID);
    insert.setString(2, vote);

    try {
      insert.executeUpdate();
    } catch (SQLException e) {
      PreparedStatement update = dbConn.prepareStatement(
        "UPDATE votes SET vote = ? WHERE id = ?");
      update.setString(1, vote);
      update.setString(2, voterID);
      update.executeUpdate();
    }
  }

  // More information about Jedis, constructor and authentication to Redis:
  // https://tool.oschina.net/uploads/apidocs/jedis-2.1.0/redis/clients/jedis/Jedis.html#Jedis(java.lang.String)
  static Jedis connectToRedis(String host, String password) {
    Jedis conn = new Jedis(host);
    conn.auth(password);

    while (true) {
      try {
        conn.keys("*");
        break;
      } catch (JedisConnectionException e) {
        System.err.println("Waiting for redis");
        sleep(1000);
      }
    }

    System.err.println("Connected to redis");
    return conn;
  }

  static Connection connectToDB(String connectionString, String pgUser, String pgPassword) throws SQLException {
    Connection conn = null;

    try {
      Class.forName("org.postgresql.Driver");      
      while (conn == null) {
        try {
          // Format: DriverManager.getConnection(url, username, password);
          conn = DriverManager.getConnection(connectionString, pgUser, pgPassword);
        } catch (SQLException ex) {
          System.err.println("Waiting for db");
          System.err.println("Error: " + ex.toString());
          sleep(1000);
        }
      }

      PreparedStatement st = conn.prepareStatement(
        "CREATE TABLE IF NOT EXISTS votes (id VARCHAR(255) NOT NULL UNIQUE, vote VARCHAR(255) NOT NULL)");
      st.executeUpdate();

    } catch (ClassNotFoundException e) {
      e.printStackTrace();
      System.exit(1);
    }

    System.err.println("Connected to db");
    return conn;
  }

  static void sleep(long duration) {
    try {
      Thread.sleep(duration);
    } catch (InterruptedException e) {
      System.exit(1);
    }
  }
}
