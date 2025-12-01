// Drop existing collections to start fresh
db = db.getSiblingDB('uchat');

print("Dropping existing collections...");
db.chats.drop();
db.messages.drop();
db.attachments.drop();
print("Collections dropped successfully");
