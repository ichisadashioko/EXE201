import { useNavigate } from "react-router";
import { api_matching_record_store_rating, api_pets_matching, getAccessToken } from "../authentication";
import { useEffect, useState } from "react";
import type { MatchingPetInfo } from "../typing";

function SwipeCard({ profile, onSwipe }: { profile: MatchingPetInfo; onSwipe: (direction: 'left' | 'right') => void }) {
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
                    src={profile.profile_image_url ?? undefined}
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
                    <h2>{profile.name}</h2>
                    <p>📍</p>
                </div>
            </div>

            {/* Info Section */}
            <div className="h-2/5 p-4 overflow-y-auto">
                <div className="space-y-3">
                    <div>
                        <h3>About {profile.name}</h3>
                        <p className="text-gray-600">{profile.desc}</p>
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
export default function Matching() {
    const navigate = useNavigate();
    const access_token = getAccessToken();
    const [pet_info_list, setPetInfoList] = useState<MatchingPetInfo[]>([]);

    const [currentIndex, setCurrentIndex] = useState(0);
    const handleSwipe = async (direction: 'left' | 'right') => {
        const profile = pet_info_list[currentIndex];
        console.log(`Swiped ${direction} on pet:`, profile);
        // TODO implement undo feature

        if (access_token == null) {
            console.error("Access token is null, cannot store matching record");
            navigate("/login");
            return;
        }

        if (profile == null) {
            console.error("Profile is null, cannot store matching record");
            return;
        }

        // TODO call API
        api_matching_record_store_rating(
            access_token!,
            profile.id,
            direction === 'left' ? -1 : 1,
        ).then((retval) => {
            console.debug(retval);
            if (retval.success) {
                console.log("Stored matching record successfully");
            } else {
                alert(`Failed to store matching record: ${retval.message}`);
            }
        }).catch((error) => {
            console.error("Error storing matching record:");
            console.error(error);
            alert(`Error storing matching record: ${error}`);
        });

        setCurrentIndex(prev => prev + 1);
    };

    const handleButtonSwipe = (direction: 'left' | 'right') => {
        handleSwipe(direction);
    };

    useEffect(() => {
        if (access_token == null) {
            console.log("Access token is null, redirecting to login page");
            navigate("/login");
            return;
        }

        async function fetchMatchingPets() {
            let retval = await api_pets_matching(
                access_token!,
            );

            console.debug(retval);

            if (retval.success) {
                try {
                    let pet_list = retval.data.pets;
                    setPetInfoList(pet_list);
                    console.log("Fetched matching pets:", pet_list);
                } catch (error) {
                    console.error("Error fetching matching pets:");
                    console.error(error);
                    alert(`Error fetching matching pets: ${error}`);
                    return;
                }
                // alert("Image uploaded successfully");
            } else {
                alert(`Failed to fetch matching pets: ${retval.message}`);
            }
        }

        fetchMatchingPets();
    }, []);

    if (currentIndex >= pet_info_list.length) {
        // TODO call API to get more pets
        // return <div>No more pets to show</div>;
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
        // <div>
        //     <h1>Matching</h1>
        //     <pre>{JSON.stringify(pet_info_list, null, 4)}</pre>
        // </div>

        <div className="min-h-screen bg-gray-100">
            <div className="container mx-auto px-4 py-6 h-screen flex flex-col">
                {/* Header */}
                <div className="text-center mb-6">
                    <h1 className="text-3xl text-purple-600">SwipeApp</h1>
                    <p className="text-gray-600">{pet_info_list.length - currentIndex} profiles remaining</p>
                </div>

                {/* Card Area */}
                <div className="flex-1 max-w-md mx-auto w-full relative">
                    {/* Stack Effect - Show next cards behind */}
                    {pet_info_list.slice(currentIndex, currentIndex + 3).map((profile, index) => (
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
                                        src={profile.profile_image_url ?? undefined}
                                        alt={profile.profile_image_url ?? "No Image"}
                                        className="w-full h-3/5 object-cover rounded-t-2xl"
                                    />
                                    <div className="p-4">
                                        <h3>{profile.name}</h3>
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
    )
}
