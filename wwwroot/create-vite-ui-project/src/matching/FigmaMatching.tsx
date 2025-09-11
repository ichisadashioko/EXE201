import { useState } from 'react';

interface Profile {
    id: string;
    name: string;
    age: number;
    image: string;
    location: string;
    bio: string;
}

const profiles: Profile[] = [
    {
        id: '1',
        name: 'Emma',
        age: 26,
        image: 'https://images.unsplash.com/photo-1520423465871-0866049020b7?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxwb3J0cmFpdCUyMHlvdW5nJTIwd29tYW4lMjBzbWlsaW5nfGVufDF8fHx8MTc1NzQ4ODAyOHww&ixlib=rb-4.1.0&q=80&w=1080',
        location: 'New York, NY',
        bio: 'Love exploring new coffee shops and hiking on weekends. Always up for an adventure!'
    },
    {
        id: '2',
        name: 'Alex',
        age: 29,
        image: 'https://images.unsplash.com/photo-1543132220-e7fef0b974e7?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxwb3J0cmFpdCUyMHlvdW5nJTIwbWFuJTIwY2FzdWFsfGVufDF8fHx8MTC1NzQ5MjgwMnww&ixlib=rb-4.1.0&q=80&w=1080',
        location: 'Brooklyn, NY',
        bio: 'Software engineer who loves cooking and playing guitar. Looking for someone to explore the city with.'
    },
    {
        id: '3',
        name: 'Sarah',
        age: 24,
        image: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxwb3J0cmFpdCUyMHdvbWFuJTIwcHJvZmVzc2lvbmFsfGVufDF8fHx8MTc1NzU2MzE1OHww&ixlib=rb-4.1.0&q=80&w=1080',
        location: 'Manhattan, NY',
        bio: 'Travel enthusiast and marketing professional. Always planning my next adventure!'
    }
];

function SwipeCard({ profile, onSwipe }: { profile: Profile; onSwipe: (direction: 'left' | 'right') => void }) {
    const [isDragging, setIsDragging] = useState(false);
    const [startX, setStartX] = useState(0);
    const [currentX, setCurrentX] = useState(0);
    const [isExpanded, setIsExpanded] = useState(false);

    const handleStart = (clientX: number) => {
        setIsDragging(true);
        setStartX(clientX);
    };

    const handleMove = (clientX: number) => {
        if (!isDragging) return;
        setCurrentX(clientX - startX);
    };

    const handleEnd = () => {
        if (!isDragging) return;
        setIsDragging(false);

        if (Math.abs(currentX) > 100) {
            onSwipe(currentX > 0 ? 'right' : 'left');
        }
        setCurrentX(0);
    };

    const rotation = currentX * 0.1;
    const opacity = 1 - Math.abs(currentX) / 300;

    return (
        <div
            className="absolute inset-4 bg-white rounded-2xl shadow-2xl overflow-hidden cursor-grab active:cursor-grabbing select-none"
            style={{
                transform: `translateX(${currentX}px) rotate(${rotation}deg)`,
                opacity: isDragging ? opacity : 1,
                transition: isDragging ? 'none' : 'all 0.3s ease'
            }}
            onMouseDown={(e) => handleStart(e.clientX)}
            onMouseMove={(e) => handleMove(e.clientX)}
            onMouseUp={handleEnd}
            onMouseLeave={handleEnd}
            onTouchStart={(e) => handleStart(e.touches[0].clientX)}
            onTouchMove={(e) => handleMove(e.touches[0].clientX)}
            onTouchEnd={handleEnd}
        >
            {/* Image Section */}
            <div className="relative h-3/5">
                <img
                    src={profile.image}
                    alt={profile.name}
                    className="w-full h-full object-cover"
                    draggable={false}
                />

                {/* Swipe Indicators */}
                {isDragging && (
                    <>
                        <div
                            className="absolute top-8 left-8 bg-green-500 text-white px-4 py-2 rounded-lg"
                            style={{ opacity: currentX > 0 ? currentX / 100 : 0 }}
                        >
                            ❤️ LIKE
                        </div>
                        <div
                            className="absolute top-8 right-8 bg-red-500 text-white px-4 py-2 rounded-lg"
                            style={{ opacity: currentX < 0 ? Math.abs(currentX) / 100 : 0 }}
                        >
                            ❌ PASS
                        </div>
                    </>
                )}

                {/* Basic Info Overlay */}
                <div className="absolute bottom-0 left-0 right-0 bg-gradient-to-t from-black/70 to-transparent p-4 text-white">
                    <h2>{profile.name}, {profile.age}</h2>
                    <p>📍 {profile.location}</p>
                </div>
            </div>

            {/* Info Section */}
            <div className="h-2/5 p-4 overflow-y-auto">
                <div className="space-y-3">
                    <div>
                        <h3>About {profile.name}</h3>
                        <p className="text-gray-600">{profile.bio}</p>
                    </div>

                    {isExpanded && (
                        <div className="space-y-2 text-gray-600">
                            <p>🎯 Looking for something serious</p>
                            <p>🎵 Loves indie music</p>
                            <p>🍕 Foodie at heart</p>
                            <p>✈️ Travel enthusiast</p>
                        </div>
                    )}

                    <button
                        onClick={() => setIsExpanded(!isExpanded)}
                        className="text-blue-500 underline"
                    >
                        {isExpanded ? 'Show less' : 'Show more'}
                    </button>
                </div>
            </div>
        </div>
    );
}

export default function FigmaMatchingApp() {
    const [currentIndex, setCurrentIndex] = useState(0);

    const handleSwipe = async (direction: 'left' | 'right') => {
        const profile = profiles[currentIndex];

        // Mock API call
        console.log(`API Call: ${direction === 'right' ? 'LIKE' : 'PASS'} for ${profile.name}`);

        // You can replace this with actual API calls:
        // await fetch('/api/swipe', {
        //   method: 'POST',
        //   body: JSON.stringify({ profileId: profile.id, action: direction })
        // });

        setCurrentIndex(prev => prev + 1);
    };

    const handleButtonSwipe = (direction: 'left' | 'right') => {
        handleSwipe(direction);
    };

    if (currentIndex >= profiles.length) {
        return (
            <div className="min-h-screen bg-gray-100 flex items-center justify-center">
                <div className="text-center">
                    <h1>No more profiles!</h1>
                    <p className="text-gray-600">Check back later for more matches.</p>
                    <button
                        onClick={() => setCurrentIndex(0)}
                        className="mt-4 bg-blue-500 text-white px-6 py-2 rounded-lg"
                    >
                        Reset Demo
                    </button>
                </div>
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-gray-100">
            <div className="container mx-auto px-4 py-6 h-screen flex flex-col">
                {/* Header */}
                <div className="text-center mb-6">
                    <h1 className="text-3xl text-purple-600">SwipeApp</h1>
                    <p className="text-gray-600">{profiles.length - currentIndex} profiles remaining</p>
                </div>

                {/* Card Area */}
                <div className="flex-1 max-w-md mx-auto w-full relative">
                    {/* Stack Effect - Show next cards behind */}
                    {profiles.slice(currentIndex, currentIndex + 3).map((profile, index) => (
                        <div
                            key={profile.id}
                            className="absolute inset-0"
                            style={{
                                zIndex: 3 - index,
                                transform: `scale(${1 - index * 0.05}) translateY(${index * 8}px)`,
                                opacity: 1 - index * 0.3
                            }}
                        >
                            {index === 0 ? (
                                <SwipeCard profile={profile} onSwipe={handleSwipe} />
                            ) : (
                                <div className="absolute inset-4 bg-white rounded-2xl shadow-lg">
                                    <img
                                        src={profile.image}
                                        alt={profile.name}
                                        className="w-full h-3/5 object-cover rounded-t-2xl"
                                    />
                                    <div className="p-4">
                                        <h3>{profile.name}, {profile.age}</h3>
                                    </div>
                                </div>
                            )}
                        </div>
                    ))}
                </div>

                {/* Action Buttons */}
                <div className="flex justify-center space-x-8 mt-6">
                    <button
                        onClick={() => handleButtonSwipe('left')}
                        className="w-16 h-16 bg-white rounded-full shadow-lg flex items-center justify-center text-2xl border-2 border-red-200 hover:border-red-400"
                    >
                        ❌
                    </button>
                    <button
                        onClick={() => handleButtonSwipe('right')}
                        className="w-16 h-16 bg-white rounded-full shadow-lg flex items-center justify-center text-2xl border-2 border-green-200 hover:border-green-400"
                    >
                        ❤️
                    </button>
                </div>

                {/* Instructions */}
                <div className="text-center mt-4 text-gray-600">
                    <p>Swipe or drag cards left/right</p>
                </div>
            </div>
        </div>
    );
}
