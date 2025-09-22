import { useEffect, useState, useRef } from 'react';
import { useParams } from 'react-router'; // Assuming you use react-router
import { HubConnection, HubConnectionBuilder } from '@microsoft/signalr';
import { getAccessToken } from '../authentication'; // You need a function to get the JWT token

interface ChatMessage {
    id: number;
    senderUserId: number;
    content: string;
    timestamp: string;
}

export default function ChatView() {
    console.debug("Rendering ChatView component");
    const { chatThreadId } = useParams<{ chatThreadId: string }>();
    const [connection, setConnection] = useState<HubConnection | null>(null);
    const [messages, setMessages] = useState<ChatMessage[]>([]);
    const [newMessage, setNewMessage] = useState('');
    const latestMessages = useRef(messages); // Ref to get latest state in callbacks

    latestMessages.current = messages;

    useEffect(() => {
        // TODO refactor this code into authentication.ts
        // Fetch message history
        fetch(`/api/chat/${chatThreadId}/messages`, {
            headers: { 'Authorization': `Bearer ${getAccessToken()}` }
        })
            .then(res => res.json())
            .then(data => setMessages(data.messages));

        // Setup SignalR connection
        const newConnection = new HubConnectionBuilder()
            .withUrl("/chathub", {
                accessTokenFactory: () => getAccessToken() || ''
            })
            .withAutomaticReconnect()
            .build();

        setConnection(newConnection);
    }, [chatThreadId]);

    useEffect(() => {
        if (connection) {
            connection.start()
                .then(() => {
                    console.log('Connected to ChatHub');
                    connection.invoke("JoinChat", parseInt(chatThreadId!));

                    connection.on("ReceiveMessage", (message) => {
                        // Use the ref here to avoid stale state
                        const updatedMessages = [...latestMessages.current, message];
                        setMessages(updatedMessages);
                    });
                })
                .catch(e => console.log('Connection failed: ', e));
        }

        // Cleanup on component unmount
        return () => {
            connection?.stop();
        };
    }, [connection, chatThreadId]);

    const sendMessage = async (e: React.FormEvent) => {
        e.preventDefault();
        if (connection && newMessage.trim()) {
            try {
                await connection.invoke("SendMessage", parseInt(chatThreadId!), newMessage);
                setNewMessage('');
            } catch (e) {
                console.error("Failed to send message: ", e);
            }
        }
    };

    console.debug(`typeof(messages): ${typeof (messages)}`);
    console.debug(`messages: ${JSON.stringify(messages)}`);

    // TODO update UI to show sender's name instead of user ID
    return (
        <div style={{ maxWidth: '600px', margin: 'auto', border: '1px solid #ccc', borderRadius: '8px', display: 'flex', flexDirection: 'column', height: '80vh' }}>
            <div style={{ flexGrow: 1, overflowY: 'auto', padding: '10px' }}>
                {messages.map(msg => (
                    <div key={msg.id} style={{ marginBottom: '10px' }}>
                        <strong>User {msg.senderUserId}:</strong> {msg.content}
                    </div>
                ))}
            </div>
            <form onSubmit={sendMessage} style={{ display: 'flex', padding: '10px', borderTop: '1px solid #ccc' }}>
                <input
                    type="text"
                    value={newMessage}
                    onChange={e => setNewMessage(e.target.value)}
                    style={{ flexGrow: 1, padding: '8px', borderRadius: '4px', border: '1px solid #ccc' }}
                    placeholder="Type a message..."
                />
                <button type="submit" style={{
                    marginLeft: '10px',
                    padding: '8px 12px',
                    borderRadius: '4px',
                    border: 'none',
                    background: '#007bff',
                    color: 'white'
                }}>Send</button>
            </form>
        </div>
    );
}
